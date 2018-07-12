/***************************************************************************/
/*                  Copyright (c) 2018 Inno Spectra Corp.                  */
/*                           ALL RIGHTS RESERVED                           */
/***************************************************************************/

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DLP_NIR_Win_SDK_CS;

namespace DLP_NIR_Win_SDK_App_CS
{
    public static class GlobalData
    {
        public static int RepeatedScanCountDown = 0;
        public static int ScannedCounts = 0;
        public static int TargetScanNumber = 0;
        public static bool UserCancelRepeatedScan = false;
    }

    public partial class MainWindow : Window
    {
        #region Declarations

        public static String ConfigDir { get; set; }
        private static int UserSelectedDeviceIndex;

        private ScanPage scanPage = new ScanPage();
        private UtilityPage utilityPage = new UtilityPage();

        public delegate void ClearScanPlots();
        public static event ClearScanPlots ClearScanPlotsEvent;

        public static event Action<int> OnScanGUIControl = null;
        private static int SendScanGUIEvent { set { OnScanGUIControl(value); } }

        public static event Action<int> OnUtilityGUIControl = null;
        private static int SendUtilityGUIEvent { set { OnUtilityGUIControl(value); } }

        public enum GUI_State
        {
            DEVICE_ON,
            DEVICE_ON_SCANTAB_SELECT,
            DEVICE_OFF,
            DEVICE_OFF_SCANTAB_SELECT,
            SCAN,
            SCAN_FINISHED,
            FW_UPDATE,
            FW_UPDATE_FINISHED,
            REFERENCE_DATA_UPDATE,
            REFERENCE_DATA_UPDATE_FINISHED,
            KEY_ACTIVATE,
            KEY_NOT_ACTIVATE,
        };

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            String version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            this.Title = "ISC NIRScan v" + version;

            // Setup the MainWindow Position to center desktop screen
            var desktopWorkingArea = SystemParameters.WorkArea;
            double thisLeft = desktopWorkingArea.Right - this.Width;
            if (thisLeft < 0)
                thisLeft = 0;
            else
                thisLeft /= 2;
            double thisTop = desktopWorkingArea.Bottom - this.Height;
            if (thisTop < 0)
                thisTop = 0;
            else
                thisTop /= 2;
            this.Left = thisLeft;
            this.Top = thisTop;

            Loaded += new RoutedEventHandler(MainWindow_Loaded);

            // Enable the CPP DLL debug output for development
            DBG.Enable_CPP_Console();

            Grid_MainWin.Children.Add(scanPage);
        }

        private void MainWindow_GUI_Handler(int state)
        {
            Boolean isEnable = false;

            switch (state)
            {
                case (int)GUI_State.DEVICE_ON:
                case (int)GUI_State.DEVICE_OFF:
                {
                    String HWRev = String.Empty;
                    if (Device.IsConnected())
                        HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;

                    if (HWRev == "D")
                    {
                        Separator_Advance.Visibility = Visibility.Visible;
                        MenuItem_Advance.Visibility = Visibility.Visible;
                        Separator_ActKeyMGMT.Visibility = Visibility.Visible;
                        MenuItem_ActKeyMGMT.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Separator_Advance.Visibility = Visibility.Collapsed;
                        MenuItem_Advance.Visibility = Visibility.Collapsed;
                        Separator_ActKeyMGMT.Visibility = Visibility.Collapsed;
                        MenuItem_ActKeyMGMT.Visibility = Visibility.Collapsed;
                    }

                    if (state == (int)GUI_State.DEVICE_ON)
                        isEnable = true;
                    else
                        isEnable = false;

                    MenuItem_Info.IsEnabled = isEnable;
                    MenuItem_ResetSys.IsEnabled = isEnable;
                    MenuItem_UpdateRef.IsEnabled = isEnable;
                    MenuItem_Advance.IsEnabled = isEnable;
                    MenuItem_ActKeyMGMT.IsEnabled = isEnable;
                    Button_ClearAllErrors.IsEnabled = isEnable;
                    break;
                }
                case (int)GUI_State.SCAN:
                case (int)GUI_State.SCAN_FINISHED:
                {
                    if (state == (int)GUI_State.SCAN)
                        isEnable = false;
                    else
                        isEnable = true;

                    MenuItem_Utility.IsEnabled = isEnable;
                    MenuItem_Device.IsEnabled = isEnable;
                    Button_ClearAllErrors.IsEnabled = isEnable;
                    break;
                }

                case (int)GUI_State.FW_UPDATE:
                case (int)GUI_State.FW_UPDATE_FINISHED:
                {
                    if (state == (int)GUI_State.FW_UPDATE)
                        isEnable = false;
                    else
                        isEnable = true;

                    MenuItem_Scan.IsEnabled = isEnable;
                    MenuItem_Device.IsEnabled = isEnable;
                    Button_ClearAllErrors.IsEnabled = isEnable;
                    break;
                }
                case (int)GUI_State.REFERENCE_DATA_UPDATE:
                case (int)GUI_State.REFERENCE_DATA_UPDATE_FINISHED:
                {
                    if (state == (int)GUI_State.REFERENCE_DATA_UPDATE)
                        isEnable = false;
                    else
                        isEnable = true;

                    MenuItem_Scan.IsEnabled = isEnable;
                    MenuItem_Utility.IsEnabled = isEnable;
                    MenuItem_Device.IsEnabled = isEnable;
                    Button_ClearAllErrors.IsEnabled = isEnable;
                    break;
                }
                default:
                    break;
            }
        }

        #region Initial Components

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Device.Init();
            MenuItem_SelectDevice.MouseEnter += new System.Windows.Input.MouseEventHandler(Enumerate_Devices);
            SDK.OnDeviceConnectionLost += new Action<bool>(Device_Disconncted_Handler);
            SDK.OnDeviceConnected += new Action<string>(Device_Connected_Handler);
            SDK.OnDeviceFound += new Action(Device_Found_Handler);
            SDK.OnDeviceError += new Action<string>(Device_Error_Handler);
            SDK.OnErrorStatusFound += new Action(RefreshErrorStatus);
            SDK.OnBeginConnectingDevice += new Action(Connecting_Device);
            SDK.OnBeginScan += new Action(BeginScan);
            SDK.OnScanCompleted += new Action(ScanCompleted);

            ScanPage.OnMainGUIControl += new Action<int>(MainWindow_GUI_Handler);
            UtilityPage.OnMainGUIControl += new Action<int>(MainWindow_GUI_Handler);

            if (!Device.IsConnected())
            {
                SDK.AutoSearch = true;
                MainWindow_GUI_Handler((int)GUI_State.DEVICE_OFF);
            }
            // Setting the interval that checks the USB connection and open device deley
            SDK.ConnectionCheckInterval = 2000;
            SDK.DeviceOpenDeley = 1000;

            LoadSettings();
        }

        private void LoadSettings()
        {
            // Config Directory
            String path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ConfigDir = Path.Combine(path, "InnoSpectra\\Config Data");

            if (Directory.Exists(ConfigDir) == false)
            {
                Directory.CreateDirectory(ConfigDir);
                DBG.WriteLine("The directory {0} was created.", ConfigDir);
            }
        }

        private void CheckFactoryRefData()
        {
            String FacRefFile = Device.DevInfo.SerialNumber + "_FacRef.dat";
            String path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            String FilePath = Path.Combine(path, "InnoSpectra\\Reference Data", FacRefFile);

            if (!File.Exists(FilePath))
                MenuItem_BackupFacRef_Click(null, null);
        }

        private void Device_Connected_Handler(String SerialNumber)
        {
            ProgressWindowCompleted();

            if (SerialNumber == null)
                DBG.WriteLine("Device connecting failed !");
            else
            {
                DBG.WriteLine("Device <{0}> connected successfullly !", SerialNumber);

                String HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;
                if ((HWRev == "D" && Device.ChkBleExist() == 1) || HWRev == "B" || HWRev == String.Empty)
                    Device.SetBluetooth(false);
                if (HWRev == "D")
                    CheckFactoryRefData();

                Device.DeviceDateTime DevDateTime = new Device.DeviceDateTime();
                DateTime Current = DateTime.Now;

                DevDateTime.Year = Current.Year;
                DevDateTime.Month = Current.Month;
                DevDateTime.Day = Current.Day;
                DevDateTime.DayOfWeek = (Int32)Current.DayOfWeek;
                DevDateTime.Hour = Current.Hour;
                DevDateTime.Minute = Current.Minute;
                DevDateTime.Second = Current.Second;
                Device.SetDateTime(DevDateTime);

                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => {
                    MainWindow_GUI_Handler((int)GUI_State.DEVICE_ON);
                    StatusIcon(1);
                    RefreshErrorStatus();

                    if (HWRev == "D")
                    {
                        ActivationKeyWindow window = new ActivationKeyWindow();
                        if (window.IsActivated)
                        {
                            StatusBarItem_DeviceStatus.Content = "Device " + Device.DevInfo.ModelName + " (" + Device.DevInfo.SerialNumber + ") connected and activated!";
                            SendScanGUIEvent = (int)GUI_State.KEY_ACTIVATE;
                            SendUtilityGUIEvent = (int)GUI_State.KEY_ACTIVATE;
                        }
                        else
                        {
                            StatusBarItem_DeviceStatus.Content = "Device " + Device.DevInfo.ModelName + " (" + Device.DevInfo.SerialNumber + ") connected but not activated!";
                            SendScanGUIEvent = (int)GUI_State.KEY_NOT_ACTIVATE;
                            SendUtilityGUIEvent = (int)GUI_State.KEY_NOT_ACTIVATE;
                        }
                    }
                    else
                    {
                        StatusBarItem_DeviceStatus.Content = "Device " + Device.DevInfo.ModelName + " (" + Device.DevInfo.SerialNumber + ") connected!";
                        SendScanGUIEvent = (int)GUI_State.KEY_NOT_ACTIVATE;
                        SendUtilityGUIEvent = (int)GUI_State.KEY_NOT_ACTIVATE;
                    }
                }));
            }
        }

        private void Device_Disconncted_Handler(bool error)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => {
                MainWindow_GUI_Handler((int)GUI_State.DEVICE_OFF);
                MenuItem_SelectDevice.Items.Clear();
                StatusIcon(0);
                StatusBarItem_DeviceStatus.Content = "Device disconnect!";
            }));

            if (error)
            {
                DBG.WriteLine("Device disconnected abnormally !");
                SDK.AutoSearch = true;
                ShowWarning("Device was disconnected !");
            }
            else
                DBG.WriteLine("Device disconnected successfully !");
        }

        #endregion

        private void MenuItem_Scan_Click(object sender, RoutedEventArgs e)
        {
            Grid_MainWin.Children.Clear();
            Grid_MainWin.Children.Add(scanPage);
        }

        private void MenuItem_Utility_Click(object sender, RoutedEventArgs e)
        {
            Grid_MainWin.Children.Clear();
            Grid_MainWin.Children.Add(utilityPage);
        }

        private void MenuItem_Info_Click(object sender, RoutedEventArgs e)
        {
            if (!Device.IsConnected())
                return;

            String UUID = BitConverter.ToString(Device.DevInfo.DeviceUUID).Replace("-", ":");
            String HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;
            String TivaRev = Device.DevInfo.TivaRev[0].ToString() + "."
                           + Device.DevInfo.TivaRev[1].ToString() + "."
                           + Device.DevInfo.TivaRev[2].ToString() + "."
                           + Device.DevInfo.TivaRev[3].ToString();
            String DLPCRev = Device.DevInfo.DLPCRev[0].ToString() + "."
                           + Device.DevInfo.DLPCRev[1].ToString() + "."
                           + Device.DevInfo.DLPCRev[2].ToString();
            String SpecLibRev = Device.DevInfo.SpecLibRev[0].ToString() + "."
                              + Device.DevInfo.SpecLibRev[1].ToString() + "."
                              + Device.DevInfo.SpecLibRev[2].ToString() + "."
                              + Device.DevInfo.SpecLibRev[3].ToString();
            String GUIRev = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            String Detector_Board_HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(2, 1) : String.Empty;
            String Lamp_Usage = "";
            if (Device.ReadLampUsage() == 0)
            {
                Lamp_Usage = GetLampUsage();
            }
            else
                Lamp_Usage = "NA";
          
            String str = "GUI Version" + "\t\t\t" + GUIRev + "\n"
                       + "Tiva SW Version" + "\t\t\t" + TivaRev + "\n"
                       + "DLPC Flash Version" + "\t\t\t" + DLPCRev + "\n"
                       + "Spectrum Library Version" + "\t\t" + SpecLibRev + "\n"
                       + "Main Board Version" + "\t\t\t" + HWRev + "\n"
                       + "Detector Board Version" + "\t\t" + Detector_Board_HWRev + "\n"
                       + "Model Name" + "\t\t\t" + Device.DevInfo.ModelName + "\n"
                       + "Device Serial Number" + "\t\t" + Device.DevInfo.SerialNumber + "\n"
                       + "Manufacturing Serial Number" + "\t\t" + Device.DevInfo.Manufacturing_SerialNumber + "\n"
                       + "Device UUID" + "\t\t\t" + UUID + "\n"
                       + "Lamp Usage" + "\t\t\t" + Lamp_Usage + "\n";
                       
            MessageBox.Show(str, "Device Information");
        }

        private String GetLampUsage()
        {
            String lampusage = "";
            UInt64 buf = Device.LampUsage / 1000;
            
            if(buf/86400!=0)
            {
                lampusage += buf / 86400 + "day ";
                buf -= 86400* (buf / 86400);
            }
            if (buf / 3600 != 0)
            {
                lampusage += buf / 3600 + "hr ";
                buf -= 3600 * (buf / 3600);
            }
            if (buf / 60 != 0)
            {
                lampusage += buf / 60 + "min ";
                buf -= 60 * (buf / 60);
            }
            lampusage += buf  + "sec ";
            return lampusage;
        }

        #region Reset System

        private void MenuItem_ResetSys_Click(object sender, RoutedEventArgs e)
        {
            if (!Device.IsConnected())
                return;

            bwTivaReset = new BackgroundWorker
            {
                WorkerReportsProgress = false,
                WorkerSupportsCancellation = true
            };
            bwTivaReset.DoWork += new DoWorkEventHandler(bwTivaReset_DoWork);
            // bwTivaReset.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwTivaReset_DoWorkCompleted);

            MessageBoxResult input = ShowQuestion("Are you sure to RESET system?", MessageBoxButton.OKCancel);
            if (input == MessageBoxResult.OK)
            {
                SendScanGUIEvent = (int)GUI_State.DEVICE_OFF_SCANTAB_SELECT;
                bwTivaReset.RunWorkerAsync();
            }
        }

        private BackgroundWorker bwTivaReset;
        private static void bwTivaReset_DoWork(object sender, DoWorkEventArgs e)
        {
            Device.ResetTiva(false);
        }
        // private static void bwTivaReset_DoWorkCompleted(object sender, RunWorkerCompletedEventArgs e) { }
        
        #endregion

        #region Update Reference Data

        private BackgroundWorker bwRefScanProgress;

        private void MenuItem_UpdateRef_Click(object sender, RoutedEventArgs e)
        {
            if (Device.IsConnected())
            {
                MessageBoxResult input;
                input = ShowQuestion("IMPORTANT!!!\n\nThis will REPLACE your FACTORY REFERENCE DATA \nand could NOT be REVERTED.\n\nAre you sure you want to do this?", MessageBoxButton.YesNo);
                if (input == MessageBoxResult.Yes)
                {
                    input = ShowQuestion("User Agreements:\n\n" +
                    "1. I am well aware of the purpose of factory reference data\n" +
                    "    and have been well trained to replace it.\n" +
                    "2. I fully understand that the factory reference data can be replaced\n" +
                    "    but not revertible.\n" +
                    "3. I agree to pay extra fee to recover the factory reference data\n" +
                    "    if I make anything wrong.\n\n" +
                    "I agree with above terms and would like to continue the process.\n"
                    , MessageBoxButton.YesNo);
                    if (input == MessageBoxResult.Yes)
                    {
                        input = ShowQuestion("IMPORTANT!!!\n\nPlease confirm again with this process.\n\nDo you still want to do this?", MessageBoxButton.YesNo);
                        if (input == MessageBoxResult.Yes)
                        {
                            input = ShowQuestion("Please place the reference sample and press 'OK' to start the reference scan...", MessageBoxButton.OKCancel);
                            if (input == MessageBoxResult.OK)
                            {
                                bwRefScanProgress = new BackgroundWorker
                                {
                                    WorkerReportsProgress = false,
                                    WorkerSupportsCancellation = true
                                };
                                bwRefScanProgress.DoWork += new DoWorkEventHandler(bwRefScanProgress_DoWork);
                                bwRefScanProgress.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwRefScanProgress_DoWorkCompleted);
                                MainWindow_GUI_Handler((int)GUI_State.REFERENCE_DATA_UPDATE);
                                SendScanGUIEvent = (int)GUI_State.REFERENCE_DATA_UPDATE;
                                SendUtilityGUIEvent = (int)GUI_State.REFERENCE_DATA_UPDATE;
                                bwRefScanProgress.RunWorkerAsync();
                            }
                            else
                                return;
                        }
                        else
                            return;
                    }
                    else
                        return;
                }
                else
                    return;
            }
            else
                ShowError("No device is connected!");
        }
        public ScanConfig.SlewScanConfig tmpCfg;//backup current config before update reference
        private void bwRefScanProgress_DoWork(object sender, DoWorkEventArgs e)
        {
            tmpCfg = ScanConfig.GetCurrentConfig();
            ScanConfig.SlewScanConfig scanCfg = new ScanConfig.SlewScanConfig();
            scanCfg.head.config_name = "UserReference";
            scanCfg.head.scan_type = 2;
            scanCfg.head.num_sections = 1;
            scanCfg.head.num_repeats = 30;
            scanCfg.section = new ScanConfig.SlewScanSection[5];
            scanCfg.section[0].section_scan_type = 0;
            scanCfg.section[0].wavelength_start_nm = 900;
            scanCfg.section[0].wavelength_end_nm = 1700;
            scanCfg.section[0].width_px = 6;
            scanCfg.section[0].num_patterns = 228;
            scanCfg.section[0].exposure_time = 0;
            
            int ret = ScanConfig.SetScanConfig(scanCfg);
            if (ret != 0)
            {
                e.Result = -3;
                return;
            }

            Thread.Sleep(200);
            ret = Scan.PerformScan(Scan.SCAN_REF_TYPE.SCAN_REF_BUILT_IN);
            if (ret == 0)
            {
                ret = Scan.SaveReferenceScan();
                if (ret == 0)
                    e.Result = 0;
                else
                    e.Result = -2;
            }
            else
                e.Result = -1;
        }

        private void bwRefScanProgress_DoWorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            int ret = (int)e.Result;
            if (ret == 0)
                ShowInfo("Reference Scan Completed Seccessfully!\n\nPlease start a new scan to check the result.");
            else if (ret == -1)
                ShowError("Scan Failed!");
            else if (ret == -2)
                ShowError("Save Reference Sacn Failed!");
            else if (ret == -3)
                ShowError("Set Reference Sacn Configuration Failed!");
            else
                ShowError("Unknow Error Occured!");

            ScanConfig.SetScanConfig(tmpCfg);//set current config after update reference
            MainWindow_GUI_Handler((int)GUI_State.REFERENCE_DATA_UPDATE_FINISHED);
            SendScanGUIEvent = (int)GUI_State.REFERENCE_DATA_UPDATE_FINISHED;
            SendUtilityGUIEvent = (int)GUI_State.REFERENCE_DATA_UPDATE_FINISHED;
        }

        private ProgressWindow pbw = null;
        public static double ProgressWindow_left = 0;//record the last window position when move
        public static double ProgressWindow_top = 0;//record the last window position when move
        private void ProgressWindowStart(string msg, bool buttonEnabled)
        {
            Thread t = new Thread(delegate ()
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() =>
                {
                    pbw = new ProgressWindow
                    {
                        info = msg,
                        ButtonEnabled = buttonEnabled,
                        Owner = this
                    };
                    pbw.Show();
                }));
            })
            {
                Priority = ThreadPriority.Highest
            };
            t.Start();
        }

        private void ProgressWindowCompleted()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() =>
            {
                try { pbw.Close();
                }
                catch { }
            }));
        }

        #endregion

        #region Select Device

        private void Enumerate_Devices(object sender, System.Windows.Input.MouseEventArgs e)
        {
            String callerName;
            if (sender != null)
                callerName = sender.GetType().ToString();
            else
                callerName = "";

            Device.Enumerate();

            String ConnectedName = String.Empty;
            if (Device.IsConnected())
            {
                if (Convert.ToInt32(Device.DevInfo.TivaRev[0]) == 2 && Convert.ToInt32(Device.DevInfo.TivaRev[1]) == 0)
                    ConnectedName = "Nirscan Nano (12345678)";
                else
                    ConnectedName = Device.DevInfo.ModelName + " (" + Device.DevInfo.SerialNumber + ")";
            }

            MenuItem_SelectDevice.Items.Clear();

            int i = 0;
            if ((callerName == "DLP_NIR_Win_SDK_App_CS.MainWindow" || callerName == "") && Device.DeviceCounts == 1)
            {
                Device.Open(Device.DeviceFound[0].SerialNumber);
                String strHeader = Device.DeviceFound[0].ProductString + " (" + Device.DeviceFound[0].SerialNumber + ")";
                MenuItem DevItem = new MenuItem { Header = strHeader };
                DevItem.Click += new RoutedEventHandler(MenuItem_Device_Connect_Click);
                MenuItem_SelectDevice.Items.Add(DevItem);
                ConnectedName = strHeader;
                DevItem.IsChecked = true;
            }
            else if (callerName != "DLP_NIR_Win_SDK_App_CS.MainWindow" && Device.DeviceCounts >= 1)
            {
                for (i = 0; i < Device.DeviceCounts; i++)
                {
                    DBG.WriteLine("USB Device [{0}]: Product Name --> {1}", i, Device.DeviceFound[i].ProductString);
                    DBG.WriteLine("USB Device [{0}]: Serial Numer --> {1}", i, Device.DeviceFound[i].SerialNumber);

                    String strHeader = Device.DeviceFound[i].ProductString + " (" + Device.DeviceFound[i].SerialNumber + ")";

                    MenuItem DevItem = new MenuItem
                    {
                        Header = strHeader
                    };
                    DevItem.Click += new RoutedEventHandler(MenuItem_Device_Connect_Click);
                    MenuItem_SelectDevice.Items.Add(DevItem);

                    if (ConnectedName == strHeader)
                        DevItem.IsChecked = true;
                }
            }
            else
                return;

            if (callerName == "DLP_NIR_Win_SDK_App_CS.MainWindow" || callerName == "" && Device.DeviceCounts > 1)
            {
                DeviceWindow deviceWindow = new DeviceWindow { Owner = this };
                deviceWindow.UserSelection += value => UserSelectedDeviceIndex = value;
                deviceWindow.ShowDialog();
                String serNum = Device.DeviceFound[UserSelectedDeviceIndex].ProductString + " (" + Device.DeviceFound[UserSelectedDeviceIndex].SerialNumber + ")";
                string[] SerNum = serNum.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                Device.Open(SerNum[1]);
                foreach (MenuItem MyMI in MenuItem_SelectDevice.Items)
                    if ((string)MyMI.Header == serNum)
                        MyMI.IsChecked = true;
            }
        }

        private void MenuItem_Device_Connect_Click(object sender, RoutedEventArgs e)
        {
            var MI = sender as MenuItem;
            String ItemName = MI.Header.ToString();
            Int32 SerNumIndexStart = ItemName.IndexOf('(') + 1;
            String Model = ItemName.Substring(0, SerNumIndexStart - 2);
            String SerNum;

            if (MI.IsChecked == true && Device.IsConnected())
                ShowWarning("Device has been already connected!");
            else
            {
                if (Device.IsConnected())
                {
                    // Manual control GUI when device closed
                    ClearScanPlotsEvent();
                    SendScanGUIEvent = (int)GUI_State.DEVICE_OFF_SCANTAB_SELECT;
                    SendScanGUIEvent = (int)GUI_State.DEVICE_OFF;
                    SendScanGUIEvent = (int)GUI_State.KEY_NOT_ACTIVATE;
                    SendUtilityGUIEvent = (int)GUI_State.DEVICE_OFF;
                    SendUtilityGUIEvent = (int)GUI_State.KEY_NOT_ACTIVATE;
                    MainWindow_GUI_Handler((int)GUI_State.DEVICE_OFF);
                    MenuItem_SelectDevice.Items.Clear();
                    StatusIcon(0);
                    StatusBarItem_DeviceStatus.Content = "Device disconnect!";

                    String HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;
                    if ((HWRev == "D" && Device.ChkBleExist() == 1) || HWRev == "B" || HWRev == String.Empty)
                        Device.SetBluetooth(true);

                    SDK.IsEnableNotify = false;  // Disable GUI notify
                    Device.Close();
                }

                foreach (MenuItem MyMI in MenuItem_SelectDevice.Items)
                    MyMI.IsChecked = false;

                if (Model == "Nirscan Nano")
                    SerNum = ItemName.Substring(SerNumIndexStart, 8);
                else
                    SerNum = ItemName.Substring(SerNumIndexStart, 7);

                SDK.IsEnableNotify = true;  // Enable GUI notify
                Device.Open(SerNum);
                MI.IsChecked = true;
            }
        }

        #endregion

        #region Advance

        private void MenuItem_BackupFacRef_Click(object sender, RoutedEventArgs e)
        {
            if (Device.IsConnected())
            {
                int ret;
                string serNum = Device.DevInfo.SerialNumber.ToString();
                ret = Device.Backup_Factory_Reference(serNum);
                if (ret < 0)
                {
                    switch (ret)
                    {
                        case -1:
                            ShowError("Factory reference data backup FAILED!\n\nOut of memory.");
                            break;
                        case -2:
                            ShowError("Factory reference data backup FAILED!\n\nSystem I/O error");
                            break;
                        case -3:
                            ShowError("Factory reference data backup FAILED!\n\nDevice communcation error");
                            break;
                        case -4:
                            ShowError("Factory reference data backup FAILED!\n\nDevice does not have the original factory reference data");
                            break;
                    }
                }
                else
                    ShowInfo("Factory reference data has been saved in local storage successfully!");
            }
            else
                ShowError("No device connected for backup factory reference!");
        }

        private void MenuItem_RestoreFacRef_Click(object sender, RoutedEventArgs e)
        {
            if (Device.IsConnected())
            {
                int ret;
                string serNum = Device.DevInfo.SerialNumber.ToString();
                ret = Device.Restore_Factory_Reference(serNum);
                if (ret < 0)
                {
                    switch (ret)
                    {
                        case -1:
                            ShowError("Factory reference data restore FAILED!\n\nOut of memory.");
                            break;
                        case -2:
                            ShowError("Factory reference data restore FAILED!\n\nBackup directory not found");
                            break;
                        case -3:
                            ShowError("Factory reference data restore FAILED!\n\nRead file error");
                            break;
                        case -4:
                            ShowError("Factory reference data restore FAILED!\n\nReference data currupted");
                            break;
                        case -5:
                            ShowError("Factory reference data restore FAILED!\n\nDevice communcation error");
                            break;
                        case -6:
                            ShowError("Factory reference data restore FAILED!\n\nData was NOT the original factory reference data");
                            break;
                    }
                }
                else
                {
                    ShowInfo("Factory reference data has been restored successfully!\n\nPlease start a new scan to check the result.");
                    ClearScanPlotsEvent();
                }
            }
            else
                ShowError("No device connected for restoring factory reference!");
        }

        #endregion

        private void MenuItem_ActKeyMGMT_Click(object sender, RoutedEventArgs e)
        {
            ActivationKeyWindow window = new ActivationKeyWindow { Owner = this };
            window.ShowDialog(); // Execution only continues here after the window is closed.

            if (window.IsActivated)
            {
                StatusBarItem_DeviceStatus.Content = "Device " + Device.DevInfo.ModelName + " (" + Device.DevInfo.SerialNumber + ") connected and activated!";
                SendScanGUIEvent = (int)GUI_State.KEY_ACTIVATE;
                SendUtilityGUIEvent = (int)GUI_State.KEY_ACTIVATE;
            }
            else
            {
                StatusBarItem_DeviceStatus.Content = "Device " + Device.DevInfo.ModelName + " (" + Device.DevInfo.SerialNumber + ") connected but not activated!";
                SendScanGUIEvent = (int)GUI_State.KEY_NOT_ACTIVATE;
                SendUtilityGUIEvent = (int)GUI_State.KEY_NOT_ACTIVATE;
            }
        }

        private void MenuItem_License_Click(object sender, RoutedEventArgs e)
        {
            LicenseWindow window = new LicenseWindow { Owner = this };
            window.ShowDialog(); // Execution only continues here after the window is closed.
        }

        private void MenuItem_AboutUs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("http://www.inno-spectra.com/");
            }
            catch { }
        }

        #region Device Status

        private void StatusIcon(Int32 status)
        {
            BitmapImage statusIcon = new BitmapImage();
            statusIcon.BeginInit();
            if (status == 1)
                statusIcon.UriSource = new Uri("Images\\Led_G.png", UriKind.Relative);
            else if (status == 0)
                statusIcon.UriSource = new Uri("Images\\Led_Gray.png", UriKind.Relative);
            else if (status == -1)
                statusIcon.UriSource = new Uri("Images\\Led_R.png", UriKind.Relative);
            statusIcon.EndInit();
            Image_StatusIcon.Stretch = Stretch.Fill;
            Image_StatusIcon.Source = statusIcon;
        }

        private void RefreshErrorStatus()
        {
            String ErrMsg = String.Empty;

            if (Device.ReadErrorStatusAndCode() != 0)
                return;

            if ((Device.ErrStatus & 0x00000001) == 0x00000001)  // Scan Error
            {
                if (Device.ErrCode[0] == 0x00000001)
                    ErrMsg += "Scan Error: DLPC150 boot error detected.    ";
                else if (Device.ErrCode[0] == 0x00000002)
                    ErrMsg += "Scan Error: DLPC150 init error detected.    ";
                else if (Device.ErrCode[0] == 0x00000004)
                    ErrMsg += "Scan Error: DLPC150 lamp driver error detected.    ";
                else if (Device.ErrCode[0] == 0x00000008)
                    ErrMsg += "Scan Error: DLPC150 crop image failed.    ";
                else if (Device.ErrCode[0] == 0x00000010)
                    ErrMsg += "Scan Error: ADC data error.    ";
                else if (Device.ErrCode[0] == 0x00000020)
                    ErrMsg += "Scan Error: Scan config Invalid.    ";
                else if (Device.ErrCode[0] == 0x00000040)
                    ErrMsg += "Scan Error: Scan pattern streaming error.    ";
                else if (Device.ErrCode[0] == 0x00000080)
                    ErrMsg += "Scan Error: DLPC150 read error.    ";
            }

            if ((Device.ErrStatus & 0x00000002) == 0x00000002)  // ADC Error
            {
                if (Device.ErrCode[1] == 0x00000001)
                    ErrMsg += "ADC Error: ADC timeout error.    ";
                else if (Device.ErrCode[1] == 0x00000002)
                    ErrMsg += "ADC Error: ADC PowerDown error.    ";
                else if (Device.ErrCode[1] == 0x00000003)
                    ErrMsg += "ADC Error: ADC PowerUp error.    ";
                else if (Device.ErrCode[1] == 0x00000004)
                    ErrMsg += "ADC Error: ADC StandBy error.    ";
                else if (Device.ErrCode[1] == 0x00000005)
                    ErrMsg += "ADC Error: ADC WAKEUP error.    ";
                else if (Device.ErrCode[1] == 0x00000006)
                    ErrMsg += "ADC Error: ADC read register error.    ";
                else if (Device.ErrCode[1] == 0x00000007)
                    ErrMsg += "ADC Error: ADC write register error.    ";
                else if (Device.ErrCode[1] == 0x00000008)
                    ErrMsg += "ADC Error: ADC configure error.    ";
                else if (Device.ErrCode[1] == 0x00000009)
                    ErrMsg += "ADC Error: ADC set buffer error.    ";
                else if (Device.ErrCode[1] == 0x0000000A)
                    ErrMsg += "ADC Error: ADC command error.    ";
            }

            if ((Device.ErrStatus & 0x00000004) == 0x00000004)  // SD Card Error
            {
                ErrMsg += "SD Card Error.    ";
            }

            if ((Device.ErrStatus & 0x00000008) == 0x00000008)  // EEPROM Error
            {
                ErrMsg += "EEPROM Error.    ";
            }

            if ((Device.ErrStatus & 0x00000010) == 0x00000010)  // BLE Error
            {
                ErrMsg += "Bluetooth Error.    ";
            }

            if ((Device.ErrStatus & 0x00000020) == 0x00000020)  // Spectrum Library Error
            {
                ErrMsg += "Spectrum Library Error.    ";
            }

            if ((Device.ErrStatus & 0x00000040) == 0x00000040)  // Hardware Error
            {
                if (Device.ErrCode[6] == 0x00000001)
                    ErrMsg += "HW Error: DLPC150 Error.    ";
            }

            if ((Device.ErrStatus & 0x00000080) == 0x00000080)  // TMP Sensor Error
            {
                if (Device.ErrCode[7] == 0x00000001)
                    ErrMsg += "TMP Error: Invalid manufacturing id.    ";
                else if (Device.ErrCode[7] == 0x00000002)
                    ErrMsg += "TMP Error: Invalid device id.    ";
                else if (Device.ErrCode[7] == 0x00000003)
                    ErrMsg += "TMP Error: Reset error.    ";
                else if (Device.ErrCode[7] == 0x00000004)
                    ErrMsg += "TMP Error: Read register error.    ";
                else if (Device.ErrCode[7] == 0x00000005)
                    ErrMsg += "TMP Error: Write register error.    ";
                else if (Device.ErrCode[7] == 0x00000006)
                    ErrMsg += "TMP Error: Timeout error.    ";
                else if (Device.ErrCode[7] == 0x00000007)
                    ErrMsg += "TMP Error: I2C error.    ";
            }

            if ((Device.ErrStatus & 0x00000100) == 0x00000100)  // HDC1000 Sensor Error
            {
                if (Device.ErrCode[8] == 0x00000001)
                    ErrMsg += "HDC1000 Error: Invalid manufacturing id.    ";
                else if (Device.ErrCode[8] == 0x00000002)
                    ErrMsg += "HDC1000 Error: Invalid device id.    ";
                else if (Device.ErrCode[8] == 0x00000003)
                    ErrMsg += "HDC1000 Error: Reset error.    ";
                else if (Device.ErrCode[8] == 0x00000004)
                    ErrMsg += "HDC1000 Error: Read register error.    ";
                else if (Device.ErrCode[8] == 0x00000005)
                    ErrMsg += "HDC1000 Error: Write register error.    ";
                else if (Device.ErrCode[8] == 0x00000006)
                    ErrMsg += "HDC1000 Error: Timeout error.    ";
                else if (Device.ErrCode[8] == 0x00000007)
                    ErrMsg += "HDC1000 Error: I2C error.    ";
            }

            if ((Device.ErrStatus & 0x00000200) == 0x00000200)  // Battery Error
            {
                if (Device.ErrCode[9] == 0x00000001)
                    ErrMsg += "Battery Error: Battery low.    ";
            }

            if ((Device.ErrStatus & 0x00000400) == 0x00000400)  // Insufficient Memory Error
            {
                ErrMsg += "Not enough memory.    ";
            }

            if ((Device.ErrStatus & 0x00000800) == 0x00000800)  // UART Error
            {
                ErrMsg += "UART error.    ";
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() =>
            {
                StatusBarItem_ErrorStatus.Content = ErrMsg;
            }));
        }

        private void Button_ClearAllErrors_Click(object sender, RoutedEventArgs e)
        {
            if (!Device.IsConnected())
                return;

            if (Device.ResetErrorStatus() == 0)
                RefreshErrorStatus();
        }

        private void Device_Found_Handler()
        {
            SDK.AutoSearch = false;
            Dispatcher.Invoke((Action)delegate ()
            {
                Enumerate_Devices(null, null);
            });
        }

        private void Device_Error_Handler(string error)
        {
            ShowWarning(error);
        }

        #endregion

        #region Progress Window

        private void Connecting_Device()
        {
            try { ProgressWindowCompleted(); } catch { }
            ProgressWindowStart("Connecting...\n\nPlease wait!", false);
        }

        private void BeginScan()
        {
            try { ProgressWindowCompleted(); } catch { }
            if (GlobalData.RepeatedScanCountDown - 1 < 1)
                ProgressWindowStart("Scanning...\n\nPlease wait!", false);
            else
            {
                string msg = string.Format("                    Scanning...\n\n{0} scans remaining, please wait!", GlobalData.TargetScanNumber - GlobalData.ScannedCounts - 1);
                ProgressWindowStart(msg, true);
            }
        }

        private void ScanCompleted()
        {
            ProgressWindowCompleted();
            if (pbw.IsCancelled)
                GlobalData.UserCancelRepeatedScan = true;
        }

        #endregion

        #region Message Box

        public static void ShowInfo(String Text)
        {
            String Title = "Information";
            MessageBoxImage Image = MessageBoxImage.Information;
            MessageBoxButton Button = MessageBoxButton.OK;

            MessageBox.Show(Text, Title, Button, Image);
        }

        public static void ShowError(String Text)
        {
            String Title = "Error!";
            MessageBoxImage Image = MessageBoxImage.Error;
            MessageBoxButton Button = MessageBoxButton.OK;

            MessageBox.Show(Text, Title, Button, Image);
        }

        public static void ShowWarning(String Text)
        {
            String Title = "Warning!";
            MessageBoxImage Image = MessageBoxImage.Warning;
            MessageBoxButton Button = MessageBoxButton.OK;

            MessageBox.Show(Text, Title, Button, Image);
        }

        public static MessageBoxResult ShowQuestion(String Text, MessageBoxButton Button)
        {
            String Title = "Question?";
            MessageBoxImage Image = MessageBoxImage.Question;
            MessageBoxResult Default;

            if (Button == MessageBoxButton.OKCancel || Button == MessageBoxButton.YesNoCancel)
            {
                Default = MessageBoxResult.Cancel;
            }
            else if (Button == MessageBoxButton.YesNo)
            {
                Default = MessageBoxResult.No;
            }
            else
            {
                Default = MessageBoxResult.None;
            }

            return MessageBox.Show(Text, Title, Button, Image, Default);
        }

        #endregion
    }
}
