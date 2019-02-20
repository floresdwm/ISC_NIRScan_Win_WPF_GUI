/***************************************************************************/
/*                  Copyright (c) 2018 Inno Spectra Corp.                  */
/*                           ALL RIGHTS RESERVED                           */
/***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using Microsoft.Win32;
using DLP_NIR_Win_SDK_CS;
using System.Timers;

namespace DLP_NIR_Win_SDK_App_CS
{
    public partial class UtilityPage : UserControl
    {
        #region Declarations

        private String Tiva_FWDir = String.Empty;
        private String DLPC_FWDir = String.Empty;

        private BackgroundWorker bwDLPCUpdate;
        private BackgroundWorker bwTivaUpdate;

        public static event Action<int> OnMainGUIControl = null;
        private static int SendMainGUIEvent { set { OnMainGUIControl(value); } }

        #endregion

        public UtilityPage()
        {
            InitializeComponent();
            initBackgroundWorker();
            Loaded += new RoutedEventHandler(UtilityPage_Loaded);
            Unloaded += new RoutedEventHandler(UtilityPage_UnLoaded);
            Dispatcher.ShutdownStarted += new EventHandler(UtilityPage_Shutdown);
            SDK.OnDeviceConnected += new Action<string>(Device_Connected_Handler);
            SDK.OnDeviceConnectionLost += new Action<bool>(Device_Disconncted_Handler);
            MainWindow.OnUtilityGUIControl += new Action<int>(UtilityPage_GUI_Handler);
        }

        private void UtilityPage_GUI_Handler(int state)
        {
            Boolean isEnable = false;

            switch (state)
            {
                case (int)MainWindow.GUI_State.DEVICE_ON:
                case (int)MainWindow.GUI_State.DEVICE_OFF:
                {
                    if (state == (int)MainWindow.GUI_State.DEVICE_ON)
                        isEnable = true;
                    else
                        isEnable = false;

                    GroupBox_ModelName.IsEnabled = isEnable;
                    GroupBox_SerialNumber.IsEnabled = isEnable;
                    GroupBox_DateTime.IsEnabled = isEnable;
                    GroupBox_LampUsage.IsEnabled = isEnable;
                    GroupBox_Sensors.IsEnabled = isEnable;
                    GroupBox_CalibCoeffs.IsEnabled = isEnable;
                    GroupBox_DLPC150FWUpdate.IsEnabled = isEnable;

                    if (TextBox_TivaFWPath.Text == String.Empty)
                        Button_TivaFWUpdate.IsEnabled = false;
                    if (TextBox_DLPC150FWPath.Text == string.Empty)
                        Button_DLPC150FWUpdate.IsEnabled = false;

                    if (CheckBox_CalWriteEnable.IsChecked == false)
                    {
                        Button_CalWriteCoeffs.IsEnabled = false;
                        Button_CalWriteGenCoeffs.IsEnabled = false;
                        Button_CalRestoreDefaultCoeffs.IsEnabled = false;
                    }

                    if (state == (int)MainWindow.GUI_State.DEVICE_ON)
                    {
                        int lastVer, curVer;
                        Byte[] latetestVerCode = { Convert.ToByte(2), Convert.ToByte(1), Convert.ToByte(0), Convert.ToByte(69) };

                        lastVer = BitConverter.ToInt32(latetestVerCode, 0);
                        curVer = BitConverter.ToInt32(Device.DevInfo.TivaRev, 0);

                        if (curVer < lastVer)
                            Button_CalRestoreDefaultCoeffs.Visibility = Visibility.Collapsed;
                        else
                            Button_CalRestoreDefaultCoeffs.Visibility = Visibility.Visible;
                    }
                    else
                        Button_CalRestoreDefaultCoeffs.Visibility = Visibility.Collapsed;

                    break;
                }
                case (int)MainWindow.GUI_State.FW_UPDATE:
                case (int)MainWindow.GUI_State.FW_UPDATE_FINISHED:
                case (int)MainWindow.GUI_State.REFERENCE_DATA_UPDATE:
                case (int)MainWindow.GUI_State.REFERENCE_DATA_UPDATE_FINISHED:
                {
                    if (state == (int)MainWindow.GUI_State.FW_UPDATE || 
                        state == (int)MainWindow.GUI_State.REFERENCE_DATA_UPDATE)
                        isEnable = false;
                    else
                        isEnable = true;

                    GroupBox_ModelName.IsEnabled = isEnable;
                    GroupBox_SerialNumber.IsEnabled = isEnable;
                    GroupBox_DateTime.IsEnabled = isEnable;
                    GroupBox_LampUsage.IsEnabled = isEnable;
                    GroupBox_Sensors.IsEnabled = isEnable;
                    GroupBox_CalibCoeffs.IsEnabled = isEnable;
                    GroupBox_TivaFWUpdate.IsEnabled = isEnable;
                    GroupBox_DLPC150FWUpdate.IsEnabled = isEnable;
                    break;
                }
                case (int)MainWindow.GUI_State.KEY_ACTIVATE:
                case (int)MainWindow.GUI_State.KEY_NOT_ACTIVATE:
                {
                    String HWRev = String.Empty, Module = String.Empty;
                    if (Device.IsConnected())
                    {
                        HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;
                        if (Device.DevInfo.ModelName.Length >= 2)
                            Module = Device.DevInfo.ModelName.Substring(Device.DevInfo.ModelName.Length - 2, 1);
                    }

                    if ((MainWindow.IsOldTivaFW() && HWRev == "D") || (!MainWindow.IsOldTivaFW() && HWRev != "A" && HWRev != String.Empty))
                    {
                        if (Module == "F")
                            GroupBox_LampUsage.Visibility = Visibility.Collapsed;
                        else
                            GroupBox_LampUsage.Visibility = Visibility.Visible;

                        int lastVer, curVer;
                        Byte[] latetestVerCode = { Convert.ToByte(2), Convert.ToByte(1), Convert.ToByte(0), Convert.ToByte(69) };

                        lastVer = BitConverter.ToInt32(latetestVerCode, 0);
                        curVer = BitConverter.ToInt32(Device.DevInfo.TivaRev, 0);

                        if (curVer < lastVer)
                            Button_CalRestoreDefaultCoeffs.Visibility = Visibility.Collapsed;
                        else
                            Button_CalRestoreDefaultCoeffs.Visibility = Visibility.Visible;

                        if (state == (int)MainWindow.GUI_State.KEY_ACTIVATE)
                        {
                            GroupBox_LampUsage.IsEnabled = true;
                            if (CheckBox_CalWriteEnable.IsChecked == true)
                                Button_CalRestoreDefaultCoeffs.IsEnabled = true;
                            else
                                Button_CalRestoreDefaultCoeffs.IsEnabled = false;
                        }
                        else
                        {
                            GroupBox_LampUsage.IsEnabled = false;
                            Button_CalRestoreDefaultCoeffs.IsEnabled = false;
                        }
                    }
                    else
                    {
                        GroupBox_LampUsage.Visibility = Visibility.Collapsed;
                        Button_CalRestoreDefaultCoeffs.Visibility = Visibility.Collapsed;
                    }
                    break;
                }
                default:
                    break;
            }
        }

        #region Initial Components

        private void UtilityPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();

            if (!Device.IsConnected())
            {
                UtilityPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF);
                UtilityPage_GUI_Handler((int)MainWindow.GUI_State.KEY_NOT_ACTIVATE);
            }
        }

        private void UtilityPage_UnLoaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void UtilityPage_Shutdown(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            String FilePath = Path.Combine(MainWindow.ConfigDir, "UtilityPageSettings.xml");
            if (File.Exists(FilePath))
            {
                XmlDocument XmlDoc = new XmlDocument();
                XmlDoc.Load(FilePath);
                XmlNode TivaDir = XmlDoc.SelectSingleNode("/Settings/TivaDir");
                Tiva_FWDir = TivaDir.InnerText;
                XmlNode DlpcDir = XmlDoc.SelectSingleNode("/Settings/DlpcDir");
                DLPC_FWDir = DlpcDir.InnerText;
            }
        }

        private void SaveSettings()
        {
            /*
             * <?xml version="1.0" encoding="utf-8"?>
             * <Settings>
             *   <TivaDir> Tiva_FWDir </TivaDir>
             *   <DlpcDir> DLPC_FWDir </DlpcDir>
             * </Settings>
             */

            if (Tiva_FWDir == String.Empty && DLPC_FWDir == String.Empty)
                return;

            XmlDocument XmlDoc = new XmlDocument();
            XmlDeclaration XmlDec = XmlDoc.CreateXmlDeclaration("1.0", "utf-8", "");
            XmlDoc.PrependChild(XmlDec);

            // Create root element
            XmlElement Root = XmlDoc.CreateElement("Settings");
            XmlDoc.AppendChild(Root);

            // Create Tiva FW path node under root element
            XmlElement TivaDir = XmlDoc.CreateElement("TivaDir");
            TivaDir.AppendChild(XmlDoc.CreateTextNode(Tiva_FWDir));
            Root.AppendChild(TivaDir);
            // Create Dlpc FW path node under root element
            XmlElement DlpcDir = XmlDoc.CreateElement("DlpcDir");
            DlpcDir.AppendChild(XmlDoc.CreateTextNode(DLPC_FWDir));
            Root.AppendChild(DlpcDir);

            // Save XML file
            String FilePath = Path.Combine(MainWindow.ConfigDir, "UtilityPageSettings.xml");
            XmlDoc.Save(FilePath);
        }

        private void Device_Connected_Handler(String SerialNumber)
        {
            if (SerialNumber == null) return;

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => {
                CheckBox_CalWriteEnable.IsChecked = false;

                UtilityPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_ON);

                TextBox_ModelName.Text = String.Empty;
                TextBox_SerialNumber.Text = String.Empty;
                TextBox_DateTime.Text = String.Empty;
                TextBox_LampUsage.Text = String.Empty;

                Label_SensorBattStatus.Content = String.Empty;
                Label_SensorBattCapacity.Content = String.Empty;
                Label_SensorHumidity.Content = String.Empty;
                Label_SensorHDCTemp.Content = String.Empty;
                Label_SensorTivaTemp.Content = String.Empty;
                Label_SensorPhotoDetector.Content = String.Empty;

                Label_CalCoeffVer.Content = String.Empty;
                Label_RefCalVer.Content = String.Empty;
                Label_ScanCfgVer.Content = String.Empty;
                TextBox_P2WCoeff0.Text = String.Empty;
                TextBox_P2WCoeff1.Text = String.Empty;
                TextBox_P2WCoeff2.Text = String.Empty;
                TextBox_ShiftVectCoeff0.Text = String.Empty;
                TextBox_ShiftVectCoeff1.Text = String.Empty;
                TextBox_ShiftVectCoeff2.Text = String.Empty;
            }));
        }

        private void Device_Disconncted_Handler(bool error)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => {
                UtilityPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF);
                UtilityPage_GUI_Handler((int)MainWindow.GUI_State.KEY_NOT_ACTIVATE);
            }));
        }

        private void initBackgroundWorker()
        {
            bwDLPCUpdate = new BackgroundWorker();
            bwTivaUpdate = new BackgroundWorker();
            bwDLPCUpdate.WorkerReportsProgress = true;
            bwTivaUpdate.WorkerReportsProgress = true;
            bwDLPCUpdate.WorkerSupportsCancellation = true;
            bwTivaUpdate.WorkerSupportsCancellation = true;
            bwDLPCUpdate.DoWork += new DoWorkEventHandler(bwDLPCUpdate_DoWork);
            bwTivaUpdate.DoWork += new DoWorkEventHandler(bwTivaUpdate_DoWork);
            bwDLPCUpdate.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwDLPCUpdate_DoWorkCompleted);
            bwTivaUpdate.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwTivaUpdate_DoSacnCompleted);
            bwDLPCUpdate.ProgressChanged += new ProgressChangedEventHandler(bwDLPCUpdate_ProgressChanged);
            bwTivaUpdate.ProgressChanged += new ProgressChangedEventHandler(bwTivaUpdate_ProgressChanged);
        }

        #endregion

        private void Button_ModelNameSet_Click(object sender, RoutedEventArgs e)
        {
            if (Device.SetModelName(Helper.CheckRegex(TextBox_ModelName.Text)) == 0)
            {
                if (Device.Information() != 0)
                {
                    DBG.WriteLine("Device Information read failed!");
                }
                else
                {
                    ActivationKeyWindow window = new ActivationKeyWindow();

                    if (window.IsActivated)
                        UtilityPage_GUI_Handler((int)MainWindow.GUI_State.KEY_ACTIVATE);
                    else
                        UtilityPage_GUI_Handler((int)MainWindow.GUI_State.KEY_NOT_ACTIVATE);
                }

                if (!String.IsNullOrEmpty(Device.DevInfo.ModelName))
                    TextBox_ModelName.Text = Device.DevInfo.ModelName;
                else
                    TextBox_ModelName.Text = "Read Failed!";
            }
            else
                TextBox_ModelName.Text = "Write Failed!";
        }

        private void Button_ModelNameGet_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder pOutBuf = new StringBuilder(128);
            
            if (Device.ReadModelName(pOutBuf) == 0)
                TextBox_ModelName.Text = pOutBuf.ToString();
            else
                TextBox_ModelName.Text = "Read Failed!";

            pOutBuf.Clear();
        }

        private void Button_SerialNumberSet_Click(object sender, RoutedEventArgs e)
        {
            String OldSerNum = Device.DevInfo.SerialNumber;

            if (Device.SetSerialNumber(Helper.CheckRegex(TextBox_SerialNumber.Text)) == 0)
            {
                if (Device.Information() != 0)
                {
                    DBG.WriteLine("Device Information read failed!");
                }
                else if (OldSerNum != Device.DevInfo.SerialNumber)
                {
                    ActivationKeyWindow window = new ActivationKeyWindow();
                    window.ChangeSerialNumber(OldSerNum, Device.DevInfo.SerialNumber);
                }

                if (!String.IsNullOrEmpty(Device.DevInfo.SerialNumber))
                    TextBox_SerialNumber.Text = Device.DevInfo.SerialNumber;
                else
                    TextBox_SerialNumber.Text = "Read Failed!";
            }
            else
                TextBox_SerialNumber.Text = "Write Failed!";
        }

        private void Button_SerialNumberGet_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder pOutBuf = new StringBuilder(128);

            if (Device.GetSerialNumber(pOutBuf) == 0)
                TextBox_SerialNumber.Text = pOutBuf.ToString();
            else
                TextBox_SerialNumber.Text = "Read Failed!";

            pOutBuf.Clear();
        }

        private void Button_DateTimeSync_Click(object sender, RoutedEventArgs e)
        {
            Device.DeviceDateTime DevDateTime = new Device.DeviceDateTime();
            DateTime Current = DateTime.Now;

            DevDateTime.Year = Current.Year;
            DevDateTime.Month = Current.Month;
            DevDateTime.Day = Current.Day;
            DevDateTime.DayOfWeek = (Int32)Current.DayOfWeek;
            DevDateTime.Hour = Current.Hour;
            DevDateTime.Minute = Current.Minute;
            DevDateTime.Second = Current.Second;

            if (Device.SetDateTime(DevDateTime) == 0)
                TextBox_DateTime.Text = Current.ToString("yyyy/M/d  H:m:s");
            else
                TextBox_DateTime.Text = "Sync Failed!";
        }

        private void Button_DateTimeGet_Click(object sender, RoutedEventArgs e)
        {
            if (Device.GetDateTime() == 0)
            {
                TextBox_DateTime.Text = Device.DevDateTime.Year + "/" 
                                      + Device.DevDateTime.Month + "/" 
                                      + Device.DevDateTime.Day + "  " 
                                      + Device.DevDateTime.Hour + ":" 
                                      + Device.DevDateTime.Minute + ":" 
                                      + Device.DevDateTime.Second;
            }
            else
                TextBox_DateTime.Text = "Get Failed!";
        }

        private void Button_LampUsageSet_Click(object sender, RoutedEventArgs e)
        {
            if (Double.TryParse(TextBox_LampUsage.Text, out Double LampUsage) == false)
            {
                TextBox_LampUsage.Text = "Not Numeric!";
                return;
            }

            if (Device.WriteLampUsage((UInt64)(LampUsage * 3600000)) == 0)  // hour to milliseconds
                Button_LampUsageGet_Click(sender, e);
            else
                TextBox_LampUsage.Text = "Write Failed!";
        }

        private void Button_LampUsageGet_Click(object sender, RoutedEventArgs e)
        {
            if (Device.ReadLampUsage() == 0)
                TextBox_LampUsage.Text = ((Double)Device.LampUsage / 3600000).ToString();  // milliseconds to hour
            else
                TextBox_LampUsage.Text = "Read Failed!";
        }

        private void Button_SensorRead_Click(object sender, RoutedEventArgs e)
        {
            if (Device.ReadSensorsData() == 0)
            {
                Label_SensorBattStatus.Content      = Device.DevSensors.BattStatus;
                Label_SensorBattCapacity.Content    = (Device.DevSensors.BattCapicity != -1)    ? (Device.DevSensors.BattCapicity.ToString() + " %")    : ("Read Failed!");
                Label_SensorHumidity.Content        = (Device.DevSensors.Humidity != -1)        ? (Device.DevSensors.Humidity.ToString() + " %")        : ("Read Failed!");
                Label_SensorHDCTemp.Content         = (Device.DevSensors.HDCTemp != -1)         ? (Device.DevSensors.HDCTemp.ToString() + " C")         : ("Read Failed!");
                Label_SensorTivaTemp.Content        = (Device.DevSensors.TivaTemp != -1)        ? (Device.DevSensors.TivaTemp.ToString() + " C")        : ("Read Failed!");
                Label_SensorPhotoDetector.Content   = (Device.DevSensors.PhotoDetector != -1)   ? (Device.DevSensors.PhotoDetector.ToString())          : ("Read Failed!");
            }
            else
            {
                Label_SensorBattStatus.Content      = "Read Failed!";
                Label_SensorBattCapacity.Content    = "Read Failed!";
                Label_SensorHumidity.Content        = "Read Failed!";
                Label_SensorHDCTemp.Content         = "Read Failed!";
                Label_SensorTivaTemp.Content        = "Read Failed!";
                Label_SensorPhotoDetector.Content   = "Read Failed!";
            }
        }

        private void CheckBox_CalWriteEnable_Click(object sender, RoutedEventArgs e)
        {
            ActivationKeyWindow window = new ActivationKeyWindow();

            if (CheckBox_CalWriteEnable.IsChecked == true)
            {
                Button_CalWriteCoeffs.IsEnabled = true;
                Button_CalWriteGenCoeffs.IsEnabled = true;
                if (window.IsActivated)
                    Button_CalRestoreDefaultCoeffs.IsEnabled = true;
                else
                    Button_CalRestoreDefaultCoeffs.IsEnabled = false;
            }
            else
            {
                Button_CalWriteCoeffs.IsEnabled = false;
                Button_CalWriteGenCoeffs.IsEnabled = false;
                Button_CalRestoreDefaultCoeffs.IsEnabled = false;
            }
        }

        private void Button_CalReadCoeffs_Click(object sender, RoutedEventArgs e)
        {
            if (Device.GetCalibStruct() == SDK.PASS)
            {
                Label_CalCoeffVer.Content       = Device.DevInfo.CalRev.ToString();
                Label_RefCalVer.Content         = Device.DevInfo.RefCalRev.ToString();
                Label_ScanCfgVer.Content        = Device.DevInfo.CfgRev.ToString();
                TextBox_P2WCoeff0.Text          = Device.Calib_Coeffs.PixelToWavelengthCoeffs[0].ToString();
                TextBox_P2WCoeff1.Text          = Device.Calib_Coeffs.PixelToWavelengthCoeffs[1].ToString();
                TextBox_P2WCoeff2.Text          = Device.Calib_Coeffs.PixelToWavelengthCoeffs[2].ToString();
                TextBox_ShiftVectCoeff0.Text    = Device.Calib_Coeffs.ShiftVectorCoeffs[0].ToString();
                TextBox_ShiftVectCoeff1.Text    = Device.Calib_Coeffs.ShiftVectorCoeffs[1].ToString();
                TextBox_ShiftVectCoeff2.Text    = Device.Calib_Coeffs.ShiftVectorCoeffs[2].ToString();
            }
            else
            {
                Label_CalCoeffVer.Content       = "0";
                Label_RefCalVer.Content         = "0";
                Label_ScanCfgVer.Content        = "0";
                TextBox_P2WCoeff0.Text          = "Read Failed!";
                TextBox_P2WCoeff1.Text          = "Read Failed!";
                TextBox_P2WCoeff2.Text          = "Read Failed!";
                TextBox_ShiftVectCoeff0.Text    = "Read Failed!";
                TextBox_ShiftVectCoeff1.Text    = "Read Failed!";
                TextBox_ShiftVectCoeff2.Text    = "Read Failed!";
            }
        }

        private void Button_CalWriteCoeffs_Click(object sender, RoutedEventArgs e)
        {
            Device.CalibCoeffs Calib_Coeffs = new Device.CalibCoeffs
            {
                PixelToWavelengthCoeffs = new Double[3],
                ShiftVectorCoeffs = new Double[3]
            };

            if ((Double.TryParse(TextBox_P2WCoeff0.Text, out Calib_Coeffs.PixelToWavelengthCoeffs[0]) == false) ||
                (Double.TryParse(TextBox_P2WCoeff1.Text, out Calib_Coeffs.PixelToWavelengthCoeffs[1]) == false) ||
                (Double.TryParse(TextBox_P2WCoeff2.Text, out Calib_Coeffs.PixelToWavelengthCoeffs[2]) == false) ||
                (Double.TryParse(TextBox_ShiftVectCoeff0.Text, out Calib_Coeffs.ShiftVectorCoeffs[0]) == false) ||
                (Double.TryParse(TextBox_ShiftVectCoeff1.Text, out Calib_Coeffs.ShiftVectorCoeffs[1]) == false) ||
                (Double.TryParse(TextBox_ShiftVectCoeff2.Text, out Calib_Coeffs.ShiftVectorCoeffs[2]) == false))
            {
                TextBox_P2WCoeff0.Text = "Not Numeric!";
                TextBox_P2WCoeff1.Text = "Not Numeric!";
                TextBox_P2WCoeff2.Text = "Not Numeric!";
                TextBox_ShiftVectCoeff0.Text = "Not Numeric!";
                TextBox_ShiftVectCoeff1.Text = "Not Numeric!";
                TextBox_ShiftVectCoeff2.Text = "Not Numeric!";
                return;
            }

            if (Device.SendCalibStruct(Calib_Coeffs) == SDK.PASS)
                Button_CalReadCoeffs_Click(sender, e);
            else
            {
                TextBox_P2WCoeff0.Text = "Write Failed!";
                TextBox_P2WCoeff1.Text = "Write Failed!";
                TextBox_P2WCoeff2.Text = "Write Failed!";
                TextBox_ShiftVectCoeff0.Text = "Write Failed!";
                TextBox_ShiftVectCoeff1.Text = "Write Failed!";
                TextBox_ShiftVectCoeff2.Text = "Write Failed!";
            }
        }

        private void Button_CalWriteGenCoeffs_Click(object sender, RoutedEventArgs e)
        {
            if (Device.SetGenericCalibStruct() == SDK.PASS)
                Button_CalReadCoeffs_Click(sender, e);
            else
            {
                TextBox_P2WCoeff0.Text = "Write Failed!";
                TextBox_P2WCoeff1.Text = "Write Failed!";
                TextBox_P2WCoeff2.Text = "Write Failed!";
                TextBox_ShiftVectCoeff0.Text = "Write Failed!";
                TextBox_ShiftVectCoeff1.Text = "Write Failed!";
                TextBox_ShiftVectCoeff2.Text = "Write Failed!";
            }
        }

        private void Button_CalRestoreDefaultCoeffs_Click(object sender, RoutedEventArgs e)
        {
            if (Device.RestoreDefaultCalibStruct() == 0)
                Button_CalReadCoeffs_Click(sender, e);
            else
            {
                TextBox_P2WCoeff0.Text = "Restore Failed!";
                TextBox_P2WCoeff1.Text = "Restore Failed!";
                TextBox_P2WCoeff2.Text = "Restore Failed!";
                TextBox_ShiftVectCoeff0.Text = "Restore Failed!";
                TextBox_ShiftVectCoeff1.Text = "Restore Failed!";
                TextBox_ShiftVectCoeff2.Text = "Restore Failed!";
            }
        }

        #region Tiva FW Update

        private void Button_TivaFWBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                InitialDirectory = (Tiva_FWDir == String.Empty) ? (Directory.GetCurrentDirectory()) : (Tiva_FWDir),
                FileName = "",                  // Default file name
                DefaultExt = ".bin",            // Default file extension
                Filter = "Binary File|*.bin"    // Filter files by extension
            };

            // Show open file dialog box
            Nullable<Boolean> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                TextBox_TivaFWPath.Text = dlg.FileName;
                Button_TivaFWUpdate.IsEnabled = true;
                Tiva_FWDir = dlg.FileName.Substring(0, dlg.FileName.LastIndexOf("\\"));
            }
        }

        private void Button_TivaFWUpdate_Click(object sender, RoutedEventArgs e)
        {
            SDK.AutoSearch = false;
            SDK.IsEnableNotify = false;
            SDK.IsConnectionChecking = false;

            String filePath = (String)TextBox_TivaFWPath.Text;

            if (Device.IsConnected() && filePath != "")
            {
                int Ret = SDK.PASS;
                int retry = 0;

                UtilityPage_GUI_Handler((int)MainWindow.GUI_State.FW_UPDATE);
                SendMainGUIEvent = (int)MainWindow.GUI_State.FW_UPDATE;

                ProgressBar_TivaFWUpdateStatus.Value = 10;
                Device.Set_Tiva_To_Bootloader();

                while (!Device.IsDFUConnected())
                {
                    if (++retry > 50)
                    {
                        Ret = SDK.FAIL;
                        break;
                    }
                    Thread.Sleep(100);
                }

                if (Ret == SDK.PASS)
                { 
                    List<object> arguments = new List<object> { filePath };
                    bwTivaUpdate.RunWorkerAsync(arguments);
                }
                else
                {
                    SDK.AutoSearch = true;
                    SDK.IsEnableNotify = true;
                    MessageBox.Show("Can not find \"Tiva DFU\"!", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                    UtilityPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF);
                    SendMainGUIEvent = (int)MainWindow.GUI_State.DEVICE_OFF;
                    SDK.IsConnectionChecking = true;
                    ProgressBar_TivaFWUpdateStatus.Value = 0;
                }

            }
            else if (Device.IsDFUConnected())
            {
                List<object> arguments = new List<object> { filePath };
                bwTivaUpdate.RunWorkerAsync(arguments);
            }
            else
            {
                SDK.AutoSearch = true;
                SDK.IsEnableNotify = true;
                MessageBox.Show("Device dose not exist or image file path error!", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                UtilityPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF);
                SendMainGUIEvent = (int)MainWindow.GUI_State.DEVICE_OFF;
                SDK.IsConnectionChecking = true;
                ProgressBar_TivaFWUpdateStatus.Value = 0;
            }
        }

        private void bwTivaUpdate_DoWork(object sender, DoWorkEventArgs e)
        {
            List<object> arguments = e.Argument as List<object>;
            String filePath = (String)arguments[0];
            bwTivaUpdate.ReportProgress(30);

            pValue = 30;
            System.Timers.Timer pTimer = new System.Timers.Timer(150);
            pTimer.Elapsed += OnTimedEvent;
            pTimer.AutoReset = true;
            pTimer.Enabled = true;
            
            int ret = Device.Tiva_FW_Update(filePath);
            if (ret == 0)
                e.Result = 0;
            else
                e.Result = ret;

            pTimer.Enabled = false;
        }

        private int pValue = 30;
        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            if (pValue < 99)
            {
                pValue += 1;
                bwTivaUpdate.ReportProgress(pValue);
            }
        }

        private void bwTivaUpdate_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int percentage = e.ProgressPercentage;
            ProgressBar_TivaFWUpdateStatus.Value = percentage;
        }

        private void bwTivaUpdate_DoSacnCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            int ret = (int)e.Result;
            ProgressBar_TivaFWUpdateStatus.Value = 100;

            UtilityPage_GUI_Handler((int)MainWindow.GUI_State.FW_UPDATE_FINISHED);
            UtilityPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF);
            UtilityPage_GUI_Handler((int)MainWindow.GUI_State.KEY_NOT_ACTIVATE);
            SendMainGUIEvent = (int)MainWindow.GUI_State.FW_UPDATE_FINISHED;

            if (ret == 0)
            {
                SendMainGUIEvent = (int)MainWindow.GUI_State.DEVICE_OFF;
                MessageBox.Show("Tiva FW updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Device.ResetTiva(true);  // Only reconnect the device
            }
            else
            {
                switch (ret)
                {
                    case -1:
                        MainWindow.ShowError("The driver, lmdfu.dll, for the USB Device Firmware Upgrade device cannot be found!");
                        break;
                    case -2:
                        MainWindow.ShowError("The driver for the USB Device Firmware Upgrade device was found but appears to be a version which this program does not support!");
                        break;
                    case -3:
                        MainWindow.ShowError("An error was reported while attempting to load the device driver for the USB Device Firmware Upgrade device!");
                        break;
                    case -4:
                        MainWindow.ShowError("Unable to open binary file.Copy binary file to a folder with Admin / read / write permission and try again.");
                        break;
                    case -5:
                        MainWindow.ShowError("Memory alloc for file read failed!");
                        break;
                    case -6:
                        MainWindow.ShowError("This image does not appear to be valid for the target device.");
                        break;
                    case -7:
                        MainWindow.ShowError("This image is not valid NIRNANO FW Image!");
                        break;
                    case -8:
                        MainWindow.ShowError("Error reported during file download!");
                        break;
                    default:
                        MainWindow.ShowError("Unknown error occured!");
                        break;
                }
            }
            SDK.AutoSearch = true;
            SDK.IsEnableNotify = true;
            SDK.IsConnectionChecking = true;
            ProgressBar_TivaFWUpdateStatus.Value = 0;
        }

        #endregion

        #region DLPC FW Update

        private void Button_DLPC150FWBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                InitialDirectory = (DLPC_FWDir == String.Empty) ? (Directory.GetCurrentDirectory()) : (DLPC_FWDir),
                FileName = "",              // Default file name
                DefaultExt = ".img",        // Default file extension
                Filter = "Image File|*.img" // Filter files by extension
            };

            // Show open file dialog box
            Nullable<Boolean> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                TextBox_DLPC150FWPath.Text = dlg.FileName;
                Button_DLPC150FWUpdate.IsEnabled = true;
                DLPC_FWDir = dlg.FileName.Substring(0, dlg.FileName.LastIndexOf("\\"));
            }
        }

        private void Button_DLPC150FWUpdate_Click(object sender, RoutedEventArgs e)
        {
            UtilityPage_GUI_Handler((int)MainWindow.GUI_State.FW_UPDATE);
            SendMainGUIEvent = (int)MainWindow.GUI_State.FW_UPDATE;

            if (Device.IsConnected() && TextBox_DLPC150FWPath.Text != "")
            {
                bwDLPCUpdate.RunWorkerAsync(TextBox_DLPC150FWPath.Text);
            }
            else
                MessageBox.Show("Device dose not exist or image file path error!", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        private void bwDLPCUpdate_DoWork(object sender, DoWorkEventArgs e)
        {
            int expectedChecksum = 0, chksum = 0, ret = 0;
            String fileName = (String)e.Argument;
            byte[] imgByteBuff = File.ReadAllBytes(fileName);
            e.Result = false;

            int dataLen = imgByteBuff.Length;

            if (!Device.DLPC_CheckSignature(imgByteBuff))
            {
                DBG.WriteLine("Invalid DLPC150 image file!");
                return;
            }

            ret = Device.DLPC_SetImageSize(dataLen);
            if (ret < 0)
            {
                DBG.WriteLine("Set DLPC150 image size failed! (error: {0})", ret);
                return;
            }

            for (int i = 0; i < dataLen; i++)
            {
                expectedChecksum += imgByteBuff[i];
            }

            Thread.Sleep(1000);

            int bytesToSend = dataLen, bytesSent = 0;
            while (bytesToSend > 0)
            {
                byte[] byteArrayToSent = new byte[bytesToSend];
                Buffer.BlockCopy(imgByteBuff, dataLen - bytesToSend, byteArrayToSent, 0, bytesToSend);

                bytesSent = Device.DLPC_FW_Update_WriteData(byteArrayToSent, bytesToSend);

                if (bytesSent < 0)
                {
                    DBG.WriteLine("DLPC150 update: Data send Failed!");
                    break;
                }

                bytesToSend -= bytesSent;

                // Report the FW update status
                float updateProgress;
                updateProgress = ((float)(dataLen - bytesToSend) / dataLen) * 100;
                bwDLPCUpdate.ReportProgress((int)updateProgress);
            }

            chksum = Device.DLPC_Get_Checksum();

            if (chksum < 0)
            {
                DBG.WriteLine("Error Reading DLPC150 Flash Checksum! (error: {0})", chksum);
            }
            else if (chksum != expectedChecksum)
            {
                DBG.WriteLine("Checksum mismatched: (Expected: {0}, DLPC Flash: {1})", expectedChecksum, chksum);
            }
            else
            {
                DBG.WriteLine("DLPC150 updated successfully!");
                e.Result = true;
            }
        }

        private void bwDLPCUpdate_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int percentage = e.ProgressPercentage;
            ProgressBar_DLPC150FWUpdateStatus.Value = e.ProgressPercentage;
        }

        private void bwDLPCUpdate_DoWorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if((bool)e.Result)
                MessageBox.Show("DLPC150 FW updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("DLPC150 FW update failed!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            ProgressBar_DLPC150FWUpdateStatus.Value = 0;
            UtilityPage_GUI_Handler((int)MainWindow.GUI_State.FW_UPDATE_FINISHED);
            SendMainGUIEvent = (int)MainWindow.GUI_State.FW_UPDATE_FINISHED;
        }

        #endregion
    }
}
