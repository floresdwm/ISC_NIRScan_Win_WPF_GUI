/***************************************************************************/
/*                  Copyright (c) 2018 Inno Spectra Corp.                  */
/*                           ALL RIGHTS RESERVED                           */
/***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Geared;
using LiveCharts.Configurations;
using LiveCharts.Defaults;
using DLP_NIR_Win_SDK_CS;
using System.Text;

namespace DLP_NIR_Win_SDK_App_CS
{
    public class ConfigurationData : INotifyPropertyChanged
    {
        private ushort _scanAvg;
        public ushort ScanAvg
        {
            get { return _scanAvg; }
            set { _scanAvg = value; OnPropertyChanged(nameof(ScanAvg)); }
        }

        private byte _pgaGain;
        public byte PGAGain
        {
            get { return _pgaGain; }
            set { _pgaGain = value; OnPropertyChanged(nameof(PGAGain)); }
        }

        private int _repeatedScanCountDown;
        public int RepeatedScanCountDown
        {
            get { return _repeatedScanCountDown; }
            set
            {
                GlobalData.RepeatedScanCountDown = value;
                _repeatedScanCountDown = value;
                OnPropertyChanged(nameof(RepeatedScanCountDown));
            }
        }

        private int _scanInterval;
        public int ScanInterval
        {
            get { return _scanInterval; }
            set { _scanInterval = value; OnPropertyChanged(nameof(ScanInterval)); }
        }

        private int _scanedCounts;
        public int scanedCounts
        {
            get { return _scanedCounts; }
            set
            {
                GlobalData.ScannedCounts = value;
                _scanedCounts = value;
            }
        }

        private int _scanCountsTarget;
        public int scanCountsTarget
        {
            get { return _scanCountsTarget; }
            set
            {
                GlobalData.TargetScanNumber = value;
                _scanCountsTarget = value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // Chart 
        public SeriesCollection SeriesCollection { get; set; }
        private string _ZoomButtonTitle;
        public string ZoomButtonTitle
        {
            get { return _ZoomButtonTitle; }
            set { _ZoomButtonTitle = value; OnPropertyChanged("ZoomButtonTitle"); }
        }

        public string _DataTooltipButtonTitle;
        public string DataTooltipButtonTitle
        {
            get { return _DataTooltipButtonTitle; }
            set { _DataTooltipButtonTitle = value; OnPropertyChanged("DataTooltipButtonTitle"); }
        }

        private string _ZoomButtonBackground;
        public string ZoomButtonBackground
        {
            get { return _ZoomButtonBackground; }
            set { _ZoomButtonBackground = value; OnPropertyChanged("ZoomButtonBackground"); }
        }

        public string _DataTooltipButtonBackground;
        public string DataTooltipButtonBackground
        {
            get { return _DataTooltipButtonBackground; }
            set { _DataTooltipButtonBackground = value; OnPropertyChanged("DataTooltipButtonBackground"); }
        }
    }

    public partial class ScanPage : UserControl
    {
        #region Declarations

        private String Scan_Dir = String.Empty;
        private String Display_Dir = String.Empty;
        private String OneScanFileName = String.Empty;
        private Int32 ScanFile_Formats = 0;

        // For Configuration
        private const Int32 MIN_WAVELENGTH = 900;
        private const Int32 MAX_WAVELENGTH = 1700;
        private const Int32 MAX_CFG_SECTION = 5;

        private List<ScanConfig.SlewScanConfig> LocalConfig = new List<ScanConfig.SlewScanConfig>();
        private List<ComboBox> ComboBox_CfgScanType = new List<ComboBox>();
        private List<ComboBox> ComboBox_CfgWidth    = new List<ComboBox>();
        private List<ComboBox> ComboBox_CfgExposure = new List<ComboBox>();
        private List<TextBox> TextBox_CfgRangeStart = new List<TextBox>();
        private List<TextBox> TextBox_CfgRangeEnd   = new List<TextBox>();
        private List<TextBox> TextBox_CfgDigRes     = new List<TextBox>();
        private List<Label> Label_CfgDigRes         = new List<Label>();
        private List<Grid> Grid_CfgSection          = new List<Grid>();

        private Int32 TargetCfg_SelIndex = -1;      // Rocord device selected config
        private Int32 LocalCfg_SelIndex = -1;       // Record local selected config
        private Boolean SelCfg_IsTarget = false;    // Record target config or local config
        private Boolean SelCfg_IsNew = false;       // Record new config or existed config

        private Int32 DevCurCfg_Index = -1;         // Record current config which set to device
        private Boolean DevCurCfg_IsTarget = false; // Record current config is target or local

        // For Scan
        private DateTime TimeScanStart = new DateTime();
        private DateTime TimeScanEnd = new DateTime();
        private static UInt32 LampStableTime = 625;
        private Scan.SCAN_REF_TYPE ReferenceSelect = Scan.SCAN_REF_TYPE.SCAN_REF_NEW;

        // Background worker for Performing Scan
        private BackgroundWorker bwScan;
        private ConfigurationData scan_Params;

        public static event Action<int> OnMainGUIControl = null;
        private static int SendMainGUIEvent { set { OnMainGUIControl(value); } }

        #endregion

        public ScanPage()
        {
            InitializeComponent();
            Loaded += new RoutedEventHandler(ScanPage_Loaded);
            Unloaded += new RoutedEventHandler(ScanPage_UnLoaded);
            Dispatcher.ShutdownStarted += new EventHandler(ScanPage_Shutdown);
            SDK.OnDeviceConnected += new Action<string>(Device_Connected_Handler);
            SDK.OnDeviceConnectionLost += new Action<bool>(Device_Disconncted_Handler);
            SDK.OnButtonScan += new Action(StartButtonScan);
            MainWindow.OnScanGUIControl += new Action<int>(ScanPage_GUI_Handler);
            scan_Params = new ConfigurationData();
            this.DataContext = scan_Params;

            InitBackgroundWorker();

            // Add delegate event to refresh the scan config list once device is connected
            MainWindow.ClearScanPlotsEvent += new MainWindow.ClearScanPlots(ClearScanPlotsUI);

            // Initial Chart
            scan_Params.SeriesCollection = new SeriesCollection(Mappers.Xy<ObservablePoint>()
                .X(point => point.X)
                .Y(point => point.Y));
            RadioButton_Intensity.IsChecked = true; // Avoid the y-axis long number at initail stage
            CheckBox_Overlay.IsChecked = false;
            MyAxisY.Title = "Intensity";
            MyAxisY.FontSize = 14;
            MyAxisX.FontSize = 14;
            MyAxisY.LabelFormatter = value => Math.Round(value, 3).ToString(); //set this to limit the label digits of axis
            MyChart.ChartLegend = null; 
            MyChart.DataTooltip = null;
            MyChart.Zoom = ZoomingOptions.None;
            scan_Params.ZoomButtonTitle = " Zoom & Pan Disabled ";
            scan_Params.ZoomButtonBackground = "#7FCCCCCC";
            scan_Params.DataTooltipButtonTitle = " Data Tooltip Disabled ";
            scan_Params.DataTooltipButtonBackground = "#7FCCCCCC";

            // Scan Config
            PopulateCfgDetailItems();
            InitCfgDetailsContent();

            // Saved Scans
            InitSavedScanCfgItems();
        }

        private void ScanPage_GUI_Handler(int state)
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

                    TabItem_ScanSetting.IsEnabled = isEnable;
                    TabItem_ScanCfg.IsEnabled = isEnable;
                    Button_Scan.IsEnabled = isEnable;
                    break;
                }
                case (int)MainWindow.GUI_State.SCAN:
                case (int)MainWindow.GUI_State.SCAN_FINISHED:
                case (int)MainWindow.GUI_State.REFERENCE_DATA_UPDATE:
                case (int)MainWindow.GUI_State.REFERENCE_DATA_UPDATE_FINISHED:
                {
                    if (state == (int)MainWindow.GUI_State.SCAN || 
                        state == (int)MainWindow.GUI_State.REFERENCE_DATA_UPDATE)
                        isEnable = false;
                    else
                        isEnable = true;

                    TabItem_ScanSetting.IsEnabled = isEnable;
                    GroupBox_RefSelect.IsEnabled = isEnable;
                    GroupBox_LampControl.IsEnabled = isEnable;
                    GroupBox_ScanAvg.IsEnabled = isEnable;
                    GroupBox_GainControl.IsEnabled = isEnable;
                    GroupBox_B2BScan.IsEnabled = isEnable;
                    GroupBox_SaveScan.IsEnabled = isEnable;

                    TabItem_ScanCfg.IsEnabled = isEnable;
                    Grid_CfgsList.IsEnabled = isEnable;
                    GroupBox_CfgDetails.IsEnabled = false;
                    Grid_CfgButton.IsEnabled = isEnable;

                    TabItem_SavedScan.IsEnabled = isEnable;
                    Grid_SavedDir.IsEnabled = isEnable;
                    ListView_SavedData.IsEnabled = isEnable;
                    Grid_SavedCfg.IsEnabled = isEnable;

                    Button_Scan.IsEnabled = isEnable;
                    break;
                }
                case (int)MainWindow.GUI_State.DEVICE_ON_SCANTAB_SELECT:
                {
                    if (TabItem_ScanSetting.IsSelected == false)
                        TabItem_ScanSetting.IsSelected = true;
                    break;
                }
                case (int)MainWindow.GUI_State.DEVICE_OFF_SCANTAB_SELECT:
                {
                    if (TabItem_SavedScan.IsSelected == false)
                        TabItem_SavedScan.IsSelected = true;
                    break;
                }
                case (int)MainWindow.GUI_State.KEY_ACTIVATE:
                case (int)MainWindow.GUI_State.KEY_NOT_ACTIVATE:
                {
                    String HWRev = String.Empty;
                    if (Device.IsConnected())
                        HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;

                    if ((MainWindow.IsOldTivaFW() && HWRev == "D") || (!MainWindow.IsOldTivaFW() && HWRev != "A" && HWRev != String.Empty))
                    {
                        if (state == (int)MainWindow.GUI_State.KEY_ACTIVATE)
                        {
                            CheckBox_LampOn.Visibility = Visibility.Collapsed;
                            RadioButton_LampOn.Visibility = Visibility.Visible;

                            RadioButton_LampOff.Visibility = Visibility.Visible;
                            RadioButton_LampStableTime.Visibility = Visibility.Visible;
                            TextBox_LampStableTime.Visibility = Visibility.Visible;

                            RadioButton_LampOff.IsEnabled = true;
                            RadioButton_LampStableTime.IsEnabled = true;
                            TextBox_LampStableTime.IsEnabled = true;

                            RadioButton_LampStableTime.IsChecked = true;
                        }
                        else
                        {
                            CheckBox_LampOn.Visibility = Visibility.Visible;
                            RadioButton_LampOn.Visibility = Visibility.Collapsed;

                            RadioButton_LampOff.Visibility = Visibility.Visible;
                            RadioButton_LampStableTime.Visibility = Visibility.Visible;
                            TextBox_LampStableTime.Visibility = Visibility.Visible;

                            RadioButton_LampOff.IsEnabled = false;
                            RadioButton_LampStableTime.IsEnabled = false;
                            TextBox_LampStableTime.IsEnabled = false;

                            RadioButton_LampStableTime.IsChecked = false;
                        }
                    }
                    else
                    {
                        CheckBox_LampOn.Visibility = Visibility.Visible;
                        RadioButton_LampOn.Visibility = Visibility.Collapsed;

                        RadioButton_LampOff.Visibility = Visibility.Collapsed;
                        RadioButton_LampStableTime.Visibility = Visibility.Collapsed;
                        TextBox_LampStableTime.Visibility = Visibility.Collapsed;

                        RadioButton_LampStableTime.IsChecked = false;
                    }

                    CheckBox_LampOn.IsChecked = false;
                    RadioButton_LampOn.IsChecked = false;
                    RadioButton_LampOff.IsChecked = false;
                    break;
                }
                default:
                    break;
            }
        }

        #region Initial Components

        private void ScanPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate directory path
            LoadSettings();
            TextBox_SaveDirPath.Text = Scan_Dir;
            TextBox_DisplayDirPath.Text = Display_Dir;
            LoadSavedScanList();  // Load saved scan list

            String Module = String.Empty;
            if (Device.IsConnected())
            {
                if (Device.DevInfo.ModelName.Length >= 2)
                    Module = Device.DevInfo.ModelName.Substring(Device.DevInfo.ModelName.Length - 2, 1);
                RadioButton_RefFac.Visibility = (Module == "F") ? Visibility.Collapsed : Visibility.Visible;
            }

            if (Device.IsConnected())
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_ON_SCANTAB_SELECT);
            else
            {
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF_SCANTAB_SELECT);
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF);
            }
        }

        private void ScanPage_UnLoaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ScanPage_Shutdown(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            String FilePath = Path.Combine(MainWindow.ConfigDir, "ScanPageSettings.xml");
            if (!File.Exists(FilePath))
            {
                String path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                Scan_Dir = Path.Combine(path, "InnoSpectra\\Scan Results");
                Display_Dir = Scan_Dir;
                ScanFile_Formats = 0x81;

                if (Directory.Exists(Scan_Dir) == false)
                {
                    Directory.CreateDirectory(Scan_Dir);
                    DBG.WriteLine("The directory {0} was created.", Scan_Dir);
                }
            }
            else
            {
                XmlDocument XmlDoc = new XmlDocument();
                XmlDoc.Load(FilePath);

                XmlNode ScanDir = XmlDoc.SelectSingleNode("/Settings/ScanDir");
                if (ScanDir.InnerText == String.Empty)
                    Scan_Dir = Path.Combine(Directory.GetCurrentDirectory(), "Scan Results");
                else
                    Scan_Dir = ScanDir.InnerText;

                XmlNode DisplayDir = XmlDoc.SelectSingleNode("/Settings/DisplayDir");
                if (DisplayDir.InnerText == String.Empty)
                    Display_Dir = Scan_Dir;
                else
                    Display_Dir = DisplayDir.InnerText;

                XmlNode FileFormats = XmlDoc.SelectSingleNode("/Settings/FileFormats");
                if (FileFormats.InnerText == String.Empty)
                    ScanFile_Formats = 0x81;
                else
                    ScanFile_Formats = Int32.Parse(FileFormats.InnerText);
            }

            CheckBox_SaveCombCSV.IsChecked  = ((ScanFile_Formats & 0x01) >> 0 == 1) ? true : false;
            CheckBox_SaveICSV.IsChecked     = ((ScanFile_Formats & 0x02) >> 1 == 1) ? true : false;
            CheckBox_SaveACSV.IsChecked     = ((ScanFile_Formats & 0x04) >> 2 == 1) ? true : false;
            CheckBox_SaveRCSV.IsChecked     = ((ScanFile_Formats & 0x08) >> 3 == 1) ? true : false;
            CheckBox_SaveIJDX.IsChecked     = ((ScanFile_Formats & 0x10) >> 4 == 1) ? true : false;
            CheckBox_SaveAJDX.IsChecked     = ((ScanFile_Formats & 0x20) >> 5 == 1) ? true : false;
            CheckBox_SaveRJDX.IsChecked     = ((ScanFile_Formats & 0x40) >> 6 == 1) ? true : false;
            CheckBox_SaveDAT.IsChecked      = ((ScanFile_Formats & 0x80) >> 7 == 1) ? true : false;
        }

        private void SaveSettings()
        {
            /*
             * <?xml version="1.0" encoding="utf-8"?>
             * <Settings>
             *   <ScanDir>     Scan_Dir     </ScanDir>
             *   <DisplayDir>  Display_Dir  </DisplayDir>
             *   <FileFormats> ScanFile_Formats </FileFormats>
             * </Settings>
             */

            if (Scan_Dir == String.Empty && Display_Dir == String.Empty && ScanFile_Formats == 0)
                return;

            XmlDocument XmlDoc = new XmlDocument();
            XmlDeclaration XmlDec = XmlDoc.CreateXmlDeclaration("1.0", "utf-8", "");
            XmlDoc.PrependChild(XmlDec);

            // Create root element
            XmlElement Root = XmlDoc.CreateElement("Settings");
            XmlDoc.AppendChild(Root);

            // Create scan dir node under root element
            XmlElement ScanDir = XmlDoc.CreateElement("ScanDir");
            ScanDir.AppendChild(XmlDoc.CreateTextNode(Scan_Dir));
            Root.AppendChild(ScanDir);

            // Create display dir node under root element
            XmlElement DisplayDir = XmlDoc.CreateElement("DisplayDir");
            DisplayDir.AppendChild(XmlDoc.CreateTextNode(Display_Dir));
            Root.AppendChild(DisplayDir);

            // Create file format node under root element
            XmlElement FileFormats = XmlDoc.CreateElement("FileFormats");
            FileFormats.AppendChild(XmlDoc.CreateTextNode(ScanFile_Formats.ToString()));
            Root.AppendChild(FileFormats);

            // Save XML file
            String FilePath = Path.Combine(MainWindow.ConfigDir, "ScanPageSettings.xml");
            XmlDoc.Save(FilePath);
        }

        private void Device_Connected_Handler(String SerialNumber)
        {
            if (SerialNumber == null) return;

            Int32 ActiveIndex = ScanConfig.GetTargetActiveScanIndex();
            if (ActiveIndex < 0) return;

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => {
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_ON);
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_ON_SCANTAB_SELECT);

                // Scan Plot Area
                SetScanConfig(ScanConfig.TargetConfig[ActiveIndex], true, ActiveIndex);

                // Scan Setting
                scan_Params.ScanAvg = ScanConfig.TargetConfig[ActiveIndex].head.num_repeats;
                scan_Params.PGAGain = 64;
                scan_Params.RepeatedScanCountDown = 0;
                scan_Params.ScanInterval = 0;
                scan_Params.scanedCounts = 0;
                RadioButton_RefNew.IsChecked = true;
                CheckBox_Overlay.IsChecked = false;
                TextBox_LampStableTime.Text = LampStableTime.ToString();
                CheckBox_AutoGain.IsChecked = true;
                CheckBox_AutoGain.IsEnabled = true;
                ComboBox_PGAGain.IsEnabled = false;

                // Scan Config
                LoadLocalCfgList();  // Load local scan config list
                RefreshTargetCfgList();  // Only refresh UI because target config list has been loaded after device opened
                GroupBox_CfgDetails.IsEnabled = false;
            }));
        }

        private void Device_Disconncted_Handler(bool error)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => {
                ClearScanPlotsUI();
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF_SCANTAB_SELECT);
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.DEVICE_OFF);
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.KEY_NOT_ACTIVATE);
            }));
        }

        private void StartButtonScan()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => {
                RadioButton_RefFac.IsChecked = true;
                Button_Scan_Click(null, null);
            }));
        }

        private void ClearScanPlotsUI()
        {
            scan_Params.SeriesCollection.Clear();
            Scan.Intensity.Clear();
            Scan.ReferenceIntensity.Clear();
            Scan.Reflectance.Clear();
            Scan.Absorbance.Clear();
            Label_ScanStatus.Content = String.Empty;
            Label_CurrentConfig.Content = String.Empty;
            Label_EstimatedScanTime.Content = String.Empty;
        }

        private void InitBackgroundWorker()
        {
            bwScan = new BackgroundWorker
            {
                WorkerReportsProgress = false,
                WorkerSupportsCancellation = true
            };
            bwScan.DoWork += new DoWorkEventHandler(bwScan_DoScan);
            bwScan.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwScan_DoSacnCompleted);
        }

        #endregion

        #region Scan Plot

        private void Button_Zoom_Click(object sender, RoutedEventArgs e)
        {
            if (scan_Params.ZoomButtonTitle == " Zoom & Pan Disabled ")
                ScanPlot_ZoomControl(true);
            else
                ScanPlot_ZoomControl(false);
        }

        private void ScanPlot_ZoomControl(Boolean enable)
        {
            if (enable)
            {
                MyChart.Zoom = ZoomingOptions.Xy;
                scan_Params.ZoomButtonTitle = " Zoom & Pan Enabled ";
                scan_Params.ZoomButtonBackground = "#7FFDA659";
            }
            else
            {
                MyChart.Zoom = ZoomingOptions.None;
                MyAxisX.MinValue = double.NaN;
                MyAxisX.MaxValue = double.NaN;
                MyAxisY.MinValue = double.NaN;
                MyAxisY.MaxValue = double.NaN;
                scan_Params.ZoomButtonTitle = " Zoom & Pan Disabled ";
                scan_Params.ZoomButtonBackground = "#7FCCCCCC";
            }
        }

        private void Button_DataHovered_Click(object sender, RoutedEventArgs e)
        {
            if (scan_Params.DataTooltipButtonTitle == " Data Tooltip Disabled ")
            {
                MyChart.DataTooltip = new DefaultTooltip();
                MyChart.Hoverable = true;
                scan_Params.DataTooltipButtonTitle = " Data Tooltip Enabled ";
                scan_Params.DataTooltipButtonBackground = "#7F62F115";
            }
            else
            {
                MyChart.DataTooltip = null;
                MyChart.Hoverable = false;
                scan_Params.DataTooltipButtonTitle = " Data Tooltip Disabled ";
                scan_Params.DataTooltipButtonBackground = "#7FCCCCCC";
            }
        }

        private void Button_Scan_Click(object sender, RoutedEventArgs e)
        {
            if (Device.IsConnected())
            {
                if (sender != null)
                    SDK.IsConnectionChecking = false;
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.SCAN);
                SendMainGUIEvent = (int)MainWindow.GUI_State.SCAN;

                scan_Params.scanedCounts = 0;
                scan_Params.scanCountsTarget = scan_Params.RepeatedScanCountDown;

                Scan.SetScanNumRepeats(scan_Params.ScanAvg);

                if (CheckBox_AutoGain.IsChecked == false)
                {
                    if (MainWindow.IsOldTivaFW() && CheckBox_LampOn.IsChecked == true)
                    {
                        Scan.SetPGAGain(scan_Params.PGAGain);
                    }
                    else
                    {
                        Scan.SetFixedPGAGain(true, scan_Params.PGAGain);
                    }                       
                }
                else
                {                    
                    if (MainWindow.IsOldTivaFW())
                        Scan.SetFixedPGAGain(false, scan_Params.PGAGain);
                    else
                        Scan.SetFixedPGAGain(true, 0); // This is set to auto PGA
                }

                if (scan_Params.RepeatedScanCountDown == 0)
                    scan_Params.RepeatedScanCountDown++;

                if (bwScan.IsBusy != true)
                    bwScan.RunWorkerAsync();
                else
                    MessageBox.Show("Scanning in progress...\n\nPlease wait!", "System Busy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
                MessageBox.Show("Please connect a device before performing scan!", "Device Not Connected", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        private void bwScan_DoScan(object sender, DoWorkEventArgs e)
        {
            DBG.WriteLine("Performing scan... Remained scans: {0}", scan_Params.RepeatedScanCountDown - 1);
            TimeScanStart = DateTime.Now;
            List<object> arguments = new List<object>();
            
            if (Scan.PerformScan(ReferenceSelect) == 0)
            {
                DBG.WriteLine("Scan completed!");
                TimeScanEnd = DateTime.Now;
                Byte pga = (Byte)Scan.GetPGAGain();
                TimeSpan ts = new TimeSpan(TimeScanEnd.Ticks - TimeScanStart.Ticks);

                arguments.Add("pass");
                arguments.Add(ts);
                arguments.Add(pga);
                e.Result = arguments;

                if (--scan_Params.RepeatedScanCountDown > 0)
                {
                    DBG.WriteLine("Wait {0} ms for next scan...", scan_Params.ScanInterval*1000);
                    Thread.Sleep(scan_Params.ScanInterval*1000);
                }
            }
            else
            {
                arguments.Add("failed");
                arguments.Add(TimeSpan.Zero);
                arguments.Add(Convert.ToByte(0));
                e.Result = arguments;
            }
        }

        private void bwScan_DoSacnCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            List<object> arguments = e.Result as List<object>;
            string result = (string)arguments[0];
            TimeSpan ts = (TimeSpan)arguments[1];
            //Byte pga = (Byte)arguments[2];//get PGA in device
            Byte pga = Scan.PGA;
            if (result != "failed")
            {
                SpectrumPlot();

                ++scan_Params.scanedCounts;
                Label_ScanCounts.Content = "Scanned: " + scan_Params.scanedCounts.ToString() + "/" + string.Format("{0}", scan_Params.scanCountsTarget == 0 ? 1 : scan_Params.scanCountsTarget);

                scan_Params.PGAGain = pga;  // If PGA is auto, it can only read the current value after scanning.
                Label_ScanStatus.Content = "Total Scan Time: " + String.Format("{0:0.000}", ts.TotalSeconds) + " secs.";

                if (ReferenceSelect != Scan.SCAN_REF_TYPE.SCAN_REF_NEW)  // Save scan results except new reference selection.
                    SaveToFiles();
                else if (Scan.IsLocalRefExist)
                {
                    RadioButton_RefPre.IsChecked = true;
                    RadioButton_RefNew.IsChecked = false;
                }

                if (GlobalData.UserCancelRepeatedScan == false && scan_Params.RepeatedScanCountDown > 0)
                {
                    /* This should be ignored since the PGA was set at scan click
                    if (CheckBox_AutoGain.IsChecked == false)
                    {
                        if (MainWindow.IsOldTivaFW() && CheckBox_LampOn.IsChecked == true)
                        {
                            Scan.SetPGAGain(scan_Params.PGAGain);
                        }
                        else
                        {
                            Scan.SetFixedPGAGain(true, scan_Params.PGAGain);
                        }
                    }
                    else
                    {
                        
                        if (MainWindow.IsOldTivaFW())
                            Scan.SetFixedPGAGain(false, scan_Params.PGAGain);
                        else
                            Scan.SetFixedPGAGain(true, 0); // This is set to auto PGA
                    }
                    */
                    bwScan.RunWorkerAsync();
                }
                else
                {
                    if (GlobalData.UserCancelRepeatedScan == true)
                    {
                        GlobalData.UserCancelRepeatedScan = false;
                        scan_Params.RepeatedScanCountDown = 0;
                    }
                    ScanPage_GUI_Handler((int)MainWindow.GUI_State.SCAN_FINISHED);
                    SendMainGUIEvent = (int)MainWindow.GUI_State.SCAN_FINISHED;
                    scan_Params.ScanInterval = 0;
                    SDK.IsConnectionChecking = true;
                }
            }
            else
            {
                scan_Params.RepeatedScanCountDown = 0;
                ScanPage_GUI_Handler((int)MainWindow.GUI_State.SCAN_FINISHED);
                SendMainGUIEvent = (int)MainWindow.GUI_State.SCAN_FINISHED;
                MainWindow.ShowError("Scan Failed!");
                if (SDK.IsConnectionChecking == false)
                    SDK.IsConnectionChecking = true;
            }
        }

        private void RadioButton_PlotMode_Checked(object sender, RoutedEventArgs e)
        {
            if (scan_Params.ZoomButtonTitle == " Zoom & Pan Enabled ")
                ScanPlot_ZoomControl(false);

            if (scan_Params.SeriesCollection.Count > 0)
            {
                int seriesCount = scan_Params.SeriesCollection.Count;
                for (int i = 0; i < seriesCount; i++)
                    scan_Params.SeriesCollection.RemoveAt(0);
            }

            var button = sender as RadioButton;

            switch (button.Name.ToString())
            {
                case "RadioButton_Absorbance":
                    MyAxisY.Title = "Absorbance (AU)";
                    break;
                case "RadioButton_Intensity":
                    MyAxisY.Title = "Intensity";
                    break;
                case "RadioButton_Reflectance":
                    MyAxisY.Title = "Reflectance";
                    break;
                case "RadioButton_Reference":
                    MyAxisY.Title = "Reference";
                    break;
                default:
                    break;
            }
            // Check if a blank series then re-plot
            if (Scan.Intensity.Count > 1)
                SpectrumPlot();
        }

        private void SpectrumPlot()
        {
            if (CheckBox_Overlay.IsChecked == false)
            {
                if (scan_Params.SeriesCollection.Count > 0)
                {
                    int seriesCount = scan_Params.SeriesCollection.Count;
                    for (int i = 0; i < seriesCount; i++)
                        scan_Params.SeriesCollection.RemoveAt(0);
                }
            }

            double[] valY = new double[Scan.ScanDataLen];
            double[] valX = new double[Scan.ScanDataLen];
            int dataCount = 0;
            string label = "";
            valX = Scan.WaveLength.ToArray();

            if ((bool)RadioButton_Intensity.IsChecked)
            {
                List<double> doubleList = Scan.Intensity.ConvertAll(x => (double)x);
                valY = doubleList.ToArray();
                label = "Intensity";
            }
            else if ((bool)RadioButton_Absorbance.IsChecked)
            {
                valY = Scan.Absorbance.ToArray();
                label = "Absorbance";
            }
            else if ((bool)RadioButton_Reflectance.IsChecked)
            {
                valY = Scan.Reflectance.ToArray();
                label = "Reflectance";
            }
            else if ((bool)RadioButton_Reference.IsChecked)
            {
                List<double> doubleList = Scan.ReferenceIntensity.ConvertAll(x => (double)x);
                valY = doubleList.ToArray();
                label = "Reference";
            }

            for (int i = 0; i < Scan.ScanDataLen; i++)
            {
                if (Double.IsNaN(valY[i]) || Double.IsInfinity(valY[i]))
                    valY[i] = 0;
            }

            if ((bool)CheckBox_Overlay.IsChecked)
            {
                for (int i = 0; i < Scan.ScanConfigData.head.num_sections; i++)
                {
                    var chartValues = new GearedValues<ObservablePoint>();
                    for (int j = 0; j < Scan.ScanConfigData.section[i].num_patterns; j++)
                        chartValues.Add(new ObservablePoint(valX[j + dataCount], valY[j + dataCount]));

                    dataCount += Scan.ScanConfigData.section[i].num_patterns;
                    scan_Params.SeriesCollection.Add(new GLineSeries
                    {
                        Values = chartValues,
                        Title = Scan.ScanConfigData.head.num_sections > 1
                        ? string.Format("#{0}->[{1}]", scan_Params.SeriesCollection.Count / Scan.ScanConfigData.head.num_sections + 1, i)
                        : string.Format("#{0}", scan_Params.SeriesCollection.Count + 1),
                        StrokeThickness = 1,
                        Fill = Brushes.Transparent, 
                        LineSmoothness = 0,
                        PointGeometry = null,
                        PointGeometrySize = 0,
                    });
                }
            }
            else
            {
                for (int i = 0; i < Scan.ScanConfigData.head.num_sections; i++)
                {
                    var chartValues = new ChartValues<ObservablePoint>();
                    for (int j = 0; j < Scan.ScanConfigData.section[i].num_patterns; j++)
                        chartValues.Add(new ObservablePoint(valX[j + dataCount], valY[j + dataCount]));

                    dataCount += Scan.ScanConfigData.section[i].num_patterns;
                    scan_Params.SeriesCollection.Add(new LineSeries
                    {
                        Values = chartValues,
                        Title = Scan.ScanConfigData.head.num_sections == 1 ? string.Format("{0}", label) : string.Format("{0}\nsection[{1}]", label, i),
                        StrokeThickness = 1,
                        Fill = null, 
                        LineSmoothness = 0,
                        PointGeometry = null,
                        PointGeometrySize = 0,
                    });
                }
            }

            // For initial the chart to avoid the crazy axis numbers
            if (Scan.ScanConfigData.head.num_sections == 0)
            {
                scan_Params.SeriesCollection.Add(new LineSeries
                {
                    Values = new ChartValues<ObservablePoint>(),
                    Title = "Intensity",
                    PointGeometry = null,
                    StrokeThickness = 1
                });
            }
        }

        private void CheckBox_Overlay_Checked(object sender, RoutedEventArgs e)
        {
            var overlay = sender as CheckBox;
            if ((bool) overlay.IsChecked)
            {
                DBG.WriteLine("Plot overlay checked");
                MyChart.DisableAnimations = true;
            }
            else
            {
                DBG.WriteLine("Plot overlay un-checked");
                MyChart.DisableAnimations = false;
            }
            scan_Params.SeriesCollection.Clear();
            if (Scan.Intensity.Count > 1)
                SpectrumPlot();
        }

        #endregion

        #region Scan Setting

        private void TabControl_ScanInfo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Device.IsConnected())
                return;

            if (e.Source is TabControl)  // To prevent subitem event trigger in tab control.
            {
                var tabItem = sender as TabControl;
                switch (tabItem.SelectedIndex)
                {
                    case 0:  // TabControl_ScanInfo
                        break;
                    case 1:  // TabItem_ScanCfg
                        if (DevCurCfg_Index > -1)
                        {
                            if (DevCurCfg_IsTarget == true)
                                ListBox_TargetCfgs.SelectedIndex = DevCurCfg_Index;
                            else
                                ListBox_LocalCfgs.SelectedIndex = DevCurCfg_Index;
                        }
                        break;
                    case 2:  // TabItem_SavedScan
                        break;
                    default:
                        break;
                }
            }
        }

        private void RadioButton_RefMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!Device.IsConnected())
                return;

            var button = sender as RadioButton;

            switch(button.Name.ToString())
            {
                case "RadioButton_RefFac":
                    ReferenceSelect = Scan.SCAN_REF_TYPE.SCAN_REF_BUILT_IN;
                    Button_Scan.Content = "Scan";
                    GroupBox_B2BScan.IsEnabled = true;
                    break;
                case "RadioButton_RefPre":
                    ReferenceSelect = Scan.SCAN_REF_TYPE.SCAN_REF_PREV;
                    Button_Scan.Content = "Scan";
                    if (!Scan.IsLocalRefExist)
                    {
                        RadioButton_RefPre.IsChecked = false;
                        RadioButton_RefNew.IsChecked = true;
                    }
                    GroupBox_B2BScan.IsEnabled = true;
                    break;
                case "RadioButton_RefNew":
                    ReferenceSelect = Scan.SCAN_REF_TYPE.SCAN_REF_NEW;
                    Button_Scan.Content = "  Scan Reference  ";
                    GroupBox_B2BScan.IsEnabled = false;
                    scan_Params.RepeatedScanCountDown = 0;
                    break;
            }
        }

        private void CheckBox_LampOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox_LampOn.IsChecked == true)
                RadioButton_LampOn_Checked(sender, e);
            else
                RadioButton_LampStableTime_Checked(sender, e);
        }

        private void RadioButton_LampOn_Checked(object sender, RoutedEventArgs e)
        {
            ActivationKeyWindow akw = new ActivationKeyWindow();
            String HWRev = String.Empty;
            if (Device.IsConnected())
                HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;

            if(MainWindow.IsOldTivaFW() || !akw.IsActivated || HWRev == "A" || HWRev == String.Empty)
            {
                CheckBox_AutoGain.IsChecked = false;
                CheckBox_AutoGain.IsEnabled = false;
                CheckBox_AutoGain_Click(sender, e);
            }
            RadioButton_Absorbance.IsEnabled = true;
            RadioButton_Reflectance.IsEnabled = true;
            TextBox_LampStableTime.IsEnabled = false;
            Scan.SetLamp(Scan.LAMP_CONTROL.ON);

            Double ScanTime = Scan.GetEstimatedScanTime();
            if (ScanTime > 0)
                Label_EstimatedScanTime.Content = "Est. Device Scan Time: " + String.Format("{0:0.000}", ScanTime) + " secs.";
        }

        private void RadioButton_LampOff_Checked(object sender, RoutedEventArgs e)
        {
            ActivationKeyWindow akw = new ActivationKeyWindow();
            String HWRev = String.Empty;
            if (Device.IsConnected())
                HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;

            if (MainWindow.IsOldTivaFW() || !akw.IsActivated || HWRev == "A" || HWRev == String.Empty)
            {
                CheckBox_AutoGain.IsChecked = false;
                CheckBox_AutoGain.IsEnabled = false;
                CheckBox_AutoGain_Click(sender, e);
            }
            RadioButton_Absorbance.IsEnabled = false;
            RadioButton_Reflectance.IsEnabled = false;
            TextBox_LampStableTime.IsEnabled = false;
            Scan.SetLamp(Scan.LAMP_CONTROL.OFF);

            Double ScanTime = Scan.GetEstimatedScanTime();
            if (ScanTime > 0)
                Label_EstimatedScanTime.Content = "Est. Device Scan Time: " + String.Format("{0:0.000}", ScanTime) + " secs.";
        }

        private void RadioButton_LampStableTime_Checked(object sender, RoutedEventArgs e)
        {
            ActivationKeyWindow akw = new ActivationKeyWindow();
            String HWRev = String.Empty;
            if (Device.IsConnected())
                HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;

            if (akw.IsActivated && HWRev != "A" && HWRev != String.Empty)
                TextBox_LampStableTime.IsEnabled = true;
            CheckBox_AutoGain.IsEnabled = true;
            CheckBox_AutoGain_Click(sender, e);
            RadioButton_Absorbance.IsEnabled = true;
            RadioButton_Reflectance.IsEnabled = true;
            Scan.SetLamp(Scan.LAMP_CONTROL.AUTO);

            Double ScanTime = Scan.GetEstimatedScanTime();
            if (ScanTime > 0)
                Label_EstimatedScanTime.Content = "Est. Device Scan Time: " + String.Format("{0:0.000}", ScanTime) + " secs.";
        }

        private void TextBox_LampStableTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (UInt32.TryParse(TextBox_LampStableTime.Text, out LampStableTime) == false)
            {
                MainWindow.ShowError("Lamp Stable Time must be numeric!");
                TextBox_LampStableTime.Text = "625";
                return;
            }

            Scan.SetLampDelay(LampStableTime);
            Double ScanTime = Scan.GetEstimatedScanTime();
            if (ScanTime > 0)
                Label_EstimatedScanTime.Content = "Est. Device Scan Time: " + String.Format("{0:0.000}", ScanTime) + " secs.";
        }

        private void Slider_ScanAvg_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ushort value = (ushort)e.NewValue;
            Scan.SetScanNumRepeats(value);

            Double ScanTime = Scan.GetEstimatedScanTime();
            if (ScanTime > 0)
                Label_EstimatedScanTime.Content = "Est. Device Scan Time: " + String.Format("{0:0.000}", ScanTime) + " secs.";
        }

        private void CheckBox_AutoGain_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox_AutoGain.IsChecked == true)
                ComboBox_PGAGain.IsEnabled = false;
            else
                ComboBox_PGAGain.IsEnabled = true;
        }

        private void TextBox_B2BScan_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TextBox_B2BScan.Text, out int repeat) && repeat > 1)
            {
                CheckBox_SaveOneCSV.IsEnabled = true;
            }
            else
            {
                CheckBox_SaveOneCSV.IsEnabled = false;
                CheckBox_SaveOneCSV.IsChecked = false;
                OneScanFileName = String.Empty;
            }
        }

        private void CheckBox_SaveFileFormat_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;

            if (checkBox.Name.ToString() == "CheckBox_SaveDAT")
            {
                if (CheckBox_SaveDAT.IsChecked == false)
                {
                    MessageBoxResult Result = MainWindow.ShowQuestion("Are you sure to cancel saving *.dat?\n" +
                                                                      "It will not be able to display in saved scan.",
                                                                      MessageBoxButton.YesNo);
                    if (Result == MessageBoxResult.No)
                        CheckBox_SaveDAT.IsChecked = true;
                }
            }

            ScanFile_Formats = (CheckBox_SaveCombCSV.IsChecked == true) ? (ScanFile_Formats | 0x01) : (ScanFile_Formats & (~0x01));
            ScanFile_Formats = (CheckBox_SaveICSV.IsChecked == true)    ? (ScanFile_Formats | 0x02) : (ScanFile_Formats & (~0x02));
            ScanFile_Formats = (CheckBox_SaveACSV.IsChecked == true)    ? (ScanFile_Formats | 0x04) : (ScanFile_Formats & (~0x04));
            ScanFile_Formats = (CheckBox_SaveRCSV.IsChecked == true)    ? (ScanFile_Formats | 0x08) : (ScanFile_Formats & (~0x08));
            ScanFile_Formats = (CheckBox_SaveIJDX.IsChecked == true)    ? (ScanFile_Formats | 0x10) : (ScanFile_Formats & (~0x10));
            ScanFile_Formats = (CheckBox_SaveAJDX.IsChecked == true)    ? (ScanFile_Formats | 0x20) : (ScanFile_Formats & (~0x20));
            ScanFile_Formats = (CheckBox_SaveRJDX.IsChecked == true)    ? (ScanFile_Formats | 0x40) : (ScanFile_Formats & (~0x40));
            ScanFile_Formats = (CheckBox_SaveDAT.IsChecked == true)     ? (ScanFile_Formats | 0x80) : (ScanFile_Formats & (~0x80));
        }

        private void Button_SaveDirChange_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = TextBox_SaveDirPath.Text
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Scan_Dir = dlg.SelectedPath;
                TextBox_SaveDirPath.Text = dlg.SelectedPath;
            }
        }

        private void TextBox_SaveDirPath_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(TextBox_SaveDirPath.Text))
            {
                MainWindow.ShowError("The directory is not exist, please check it again.");
                TextBox_SaveDirPath.Text = Scan_Dir;
            }
            else
                Scan_Dir = TextBox_SaveDirPath.Text;
        }
        #endregion

        #region Scan Configuration

        private void PopulateCfgDetailItems()
        {
            ComboBox_CfgScanType.Clear();
            ComboBox_CfgScanType.Add(ComboBox_CfgScanType1);
            ComboBox_CfgScanType.Add(ComboBox_CfgScanType2);
            ComboBox_CfgScanType.Add(ComboBox_CfgScanType3);
            ComboBox_CfgScanType.Add(ComboBox_CfgScanType4);
            ComboBox_CfgScanType.Add(ComboBox_CfgScanType5);
            ComboBox_CfgWidth.Clear();
            ComboBox_CfgWidth.Add(ComboBox_CfgWidth1);
            ComboBox_CfgWidth.Add(ComboBox_CfgWidth2);
            ComboBox_CfgWidth.Add(ComboBox_CfgWidth3);
            ComboBox_CfgWidth.Add(ComboBox_CfgWidth4);
            ComboBox_CfgWidth.Add(ComboBox_CfgWidth5);
            ComboBox_CfgExposure.Clear();
            ComboBox_CfgExposure.Add(ComboBox_CfgExposure1);
            ComboBox_CfgExposure.Add(ComboBox_CfgExposure2);
            ComboBox_CfgExposure.Add(ComboBox_CfgExposure3);
            ComboBox_CfgExposure.Add(ComboBox_CfgExposure4);
            ComboBox_CfgExposure.Add(ComboBox_CfgExposure5);
            TextBox_CfgRangeStart.Clear();
            TextBox_CfgRangeStart.Add(TextBox_CfgRangeStart1);
            TextBox_CfgRangeStart.Add(TextBox_CfgRangeStart2);
            TextBox_CfgRangeStart.Add(TextBox_CfgRangeStart3);
            TextBox_CfgRangeStart.Add(TextBox_CfgRangeStart4);
            TextBox_CfgRangeStart.Add(TextBox_CfgRangeStart5);
            TextBox_CfgRangeEnd.Clear();
            TextBox_CfgRangeEnd.Add(TextBox_CfgRangeEnd1);
            TextBox_CfgRangeEnd.Add(TextBox_CfgRangeEnd2);
            TextBox_CfgRangeEnd.Add(TextBox_CfgRangeEnd3);
            TextBox_CfgRangeEnd.Add(TextBox_CfgRangeEnd4);
            TextBox_CfgRangeEnd.Add(TextBox_CfgRangeEnd5);
            TextBox_CfgDigRes.Clear();
            TextBox_CfgDigRes.Add(TextBox_CfgDigRes1);
            TextBox_CfgDigRes.Add(TextBox_CfgDigRes2);
            TextBox_CfgDigRes.Add(TextBox_CfgDigRes3);
            TextBox_CfgDigRes.Add(TextBox_CfgDigRes4);
            TextBox_CfgDigRes.Add(TextBox_CfgDigRes5);
            Label_CfgDigRes.Clear();
            Label_CfgDigRes.Add(Label_CfgDigRes1);
            Label_CfgDigRes.Add(Label_CfgDigRes2);
            Label_CfgDigRes.Add(Label_CfgDigRes3);
            Label_CfgDigRes.Add(Label_CfgDigRes4);
            Label_CfgDigRes.Add(Label_CfgDigRes5);
            Grid_CfgSection.Clear();
            Grid_CfgSection.Add(Grid_CfgSection1);
            Grid_CfgSection.Add(Grid_CfgSection2);
            Grid_CfgSection.Add(Grid_CfgSection3);
            Grid_CfgSection.Add(Grid_CfgSection4);
            Grid_CfgSection.Add(Grid_CfgSection5);

            for (Int32 i = 0; i < MAX_CFG_SECTION; i++)
            {
                // Initialize combobox items
                for (Int32 j = 0; j < 2; j++)
                {
                    String Type = Helper.ScanTypeIndexToMode(j).Substring(0, 3);
                    ComboBox_CfgScanType[i].Items.Add(Type);
                }
                for (Int32 j = 0; j < Helper.CfgWidthItemsCount(); j++)
                {
                    Double WidthNM = Helper.CfgWidthIndexToNM(j);
                    ComboBox_CfgWidth[i].Items.Add(Math.Round(WidthNM, 2));
                }
                for (Int32 j = 0; j < Helper.CfgExpItemsCount(); j++)
                {
                    Double ExpTime = Helper.CfgExpIndexToTime(j);
                    ComboBox_CfgExposure[i].Items.Add(ExpTime);
                }
            }
        }

        private void InitCfgDetailsContent()
        {
            InitCfgDetailsBgColor();

            TextBox_CfgName.Clear();
            TextBox_CfgAvg.Clear();
            TextBox_CfgNumSections.Clear();

            for (Int32 i = 0; i < MAX_CFG_SECTION; i++)
            {
                ComboBox_CfgScanType[i].SelectedIndex = 0;
                TextBox_CfgRangeStart[i].Clear();
                TextBox_CfgRangeEnd[i].Clear();
                ComboBox_CfgWidth[i].SelectedIndex = 5;
                ComboBox_CfgExposure[i].SelectedIndex = 0;
                TextBox_CfgDigRes[i].Clear();
                Label_CfgDigRes[i].Content = String.Empty;
            }
        }

        private void InitCfgDetailsBgColor()
        {
            TextBox_CfgName.Background = Brushes.White;
            TextBox_CfgAvg.Background = Brushes.White;
            TextBox_CfgNumSections.Background = Brushes.White;

            for (Int32 i = 0; i < MAX_CFG_SECTION; i++)
            {
                TextBox_CfgRangeStart[i].Background = Brushes.White;
                TextBox_CfgRangeEnd[i].Background = Brushes.White;
                TextBox_CfgDigRes[i].Background = Brushes.White;
            }
        }

        private void FillCfgDetailsContent()
        {
            Int32 i, NumSection = 0;
            ScanConfig.SlewScanConfig CurConfig = new ScanConfig.SlewScanConfig
            {
                section = new ScanConfig.SlewScanSection[5]
            };

            InitCfgDetailsContent();

            if (SelCfg_IsTarget == true)
                CurConfig = ScanConfig.TargetConfig[TargetCfg_SelIndex];
            else
                CurConfig = LocalConfig[LocalCfg_SelIndex];

            NumSection = (CurConfig.head.num_sections <= MAX_CFG_SECTION) ? CurConfig.head.num_sections : MAX_CFG_SECTION;

            TextBox_CfgName.Text        = CurConfig.head.config_name;
            TextBox_CfgAvg.Text         = CurConfig.head.num_repeats.ToString();
            TextBox_CfgNumSections.Text = NumSection.ToString();

            for (i = 0; i < NumSection; i++)
            {
                ComboBox_CfgScanType[i].SelectedIndex   = CurConfig.section[i].section_scan_type;
                TextBox_CfgRangeStart[i].Text           = CurConfig.section[i].wavelength_start_nm.ToString();
                TextBox_CfgRangeEnd[i].Text             = CurConfig.section[i].wavelength_end_nm.ToString();
                ComboBox_CfgWidth[i].SelectedIndex      = (Helper.CfgWidthPixelToIndex(CurConfig.section[i].width_px) > -1) ? (Helper.CfgWidthPixelToIndex(CurConfig.section[i].width_px)) : (5) ;
                TextBox_CfgDigRes[i].Text               = CurConfig.section[i].num_patterns.ToString();
                ComboBox_CfgExposure[i].SelectedIndex   = CurConfig.section[i].exposure_time;
            }
        }

        private void RefreshCfgSectionItems()
        {
            for (Int32 i = 0; i < MAX_CFG_SECTION; i++)
            {
                Grid_CfgSection[i].IsEnabled = false;
            }

            if (UInt16.TryParse(TextBox_CfgNumSections.Text, out UInt16 NumSections) == true)
            {
                if (NumSections < 1)
                {
                    NumSections = 1;
                    TextBox_CfgNumSections.Text = "1";
                }
                else if (NumSections > MAX_CFG_SECTION)
                {
                    NumSections = MAX_CFG_SECTION;
                    TextBox_CfgNumSections.Text = "5";
                }

                for (Int32 i = 0; i < NumSections; i++)
                {
                    Grid_CfgSection[i].IsEnabled = true;
                }
            }
        }

        private void LoadLocalCfgList()
        {
            String FileName = Path.Combine(MainWindow.ConfigDir, "ConfigList.xml");

            if (File.Exists(FileName) == true)
            {
                XmlSerializer xml = new XmlSerializer(typeof(List<ScanConfig.SlewScanConfig>));
                TextReader reader = new StreamReader(FileName);
                LocalConfig = (List<ScanConfig.SlewScanConfig>)xml.Deserialize(reader);
                reader.Close();

                RefreshLocalCfgList();
            }
        }

        private void RefreshLocalCfgList()
        {
            ListBox_LocalCfgs.Items.Clear();
            if(LocalConfig.Count > 0)
            {
                for (Int32 i = 0; i < LocalConfig.Count; i++)
                {
                    ListBoxItem Item = new ListBoxItem
                    {
                        Content = LocalConfig[i].head.config_name
                    };
                    if (DevCurCfg_IsTarget == false)
                        Item.Foreground = (DevCurCfg_Index == i) ? Brushes.DarkOrange : Brushes.Black;
                    Item.MouseDoubleClick += new System.Windows.Input.MouseButtonEventHandler(ListBoxItem_LocalConfig_MouseDoubleClick);
                    ListBox_LocalCfgs.Items.Add(Item);
                }
            }
        }

        private void RefreshTargetCfgList()
        {
            Int32 ActiveIndex = ScanConfig.GetTargetActiveScanIndex();

            ListBox_TargetCfgs.Items.Clear();
            if (ScanConfig.TargetConfig.Count > 0)
            {
                for (Int32 i = 0; i < ScanConfig.TargetConfig.Count; i++)
                {
                    ListBoxItem Item = new ListBoxItem
                    {
                        Content = ScanConfig.TargetConfig[i].head.config_name,
                        // FontWeight = (ActiveIndex == i) ? FontWeights.Bold : FontWeights.Normal
                    };
                    if (DevCurCfg_IsTarget == true)
                        Item.Foreground = (DevCurCfg_Index == i) ? Brushes.DarkOrange : Brushes.Black;
                    Item.SetValue(TextBlock.FontStyleProperty, (ActiveIndex == i) ? FontStyles.Italic : FontStyles.Normal);
                    Item.MouseDoubleClick += new System.Windows.Input.MouseButtonEventHandler(ListBoxItem_TargetConfig_MouseDoubleClick);
                    ListBox_TargetCfgs.Items.Add(Item);
                }
            }
        }

        private void SetScanConfig(ScanConfig.SlewScanConfig Config, Boolean IsTarget, Int32 index)
        {
            ClearScanPlotsUI();

            if (ScanConfig.SetScanConfig(Config) == SDK.FAIL)
            {
                if (IsTarget)
                    MainWindow.ShowError("Device config (" + Config.head.config_name + ") is not correct, please check it again!");
                else
                    MainWindow.ShowError("Local config (" + Config.head.config_name + ") is not correct, please check it again!");
            }
            else
            {
                DevCurCfg_Index = index;
                DevCurCfg_IsTarget = IsTarget;

                if (IsTarget)
                    Label_CurrentConfig.Content = "Device Config: " + Config.head.config_name;
                else
                    Label_CurrentConfig.Content = "Local Config: " + Config.head.config_name;
                Double ScanTime = Scan.GetEstimatedScanTime();
                if (ScanTime > 0)
                    Label_EstimatedScanTime.Content = "Est. Device Scan Time: " + String.Format("{0:0.000}", ScanTime) + " secs.";

                Button_Scan.IsEnabled = true;
            }
        }

        private void ListBoxItem_LocalConfig_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetScanConfig(LocalConfig[LocalCfg_SelIndex], false, LocalCfg_SelIndex);
            scan_Params.ScanAvg = LocalConfig[LocalCfg_SelIndex].head.num_repeats;
            RefreshLocalCfgList();
            RefreshTargetCfgList();
            ListBox_LocalCfgs.SelectedIndex = DevCurCfg_Index;
        }

        private void ListBoxItem_TargetConfig_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetScanConfig(ScanConfig.TargetConfig[TargetCfg_SelIndex], true, TargetCfg_SelIndex);
            scan_Params.ScanAvg = ScanConfig.TargetConfig[TargetCfg_SelIndex].head.num_repeats;
            RefreshLocalCfgList();
            RefreshTargetCfgList();
            ListBox_TargetCfgs.SelectedIndex = DevCurCfg_Index;
        }

        private Int32 IsCfgLegal(Boolean IsColored)
        {
            Int32 ret = SDK.PASS;
            Int32 TotalPatterns = 0;
            ScanConfig.SlewScanConfig CurConfig = new ScanConfig.SlewScanConfig
            {
                section = new ScanConfig.SlewScanSection[5]
            };
            CurConfig.head.scan_type = 2;

            // Config Name
            if (TextBox_CfgName.Text == String.Empty)
            {
                if (IsColored) TextBox_CfgName.Background = Brushes.LightPink;
                ret = SDK.FAIL;
            }
            else
            {
                if (IsColored) TextBox_CfgName.Background = Brushes.White;
                CurConfig.head.config_name = Helper.CheckRegex(TextBox_CfgName.Text);
            }

            // Num Scans to Average
            if (UInt16.TryParse(TextBox_CfgAvg.Text, out CurConfig.head.num_repeats) == false || CurConfig.head.num_repeats == 0)
            {
                if (IsColored) TextBox_CfgAvg.Background = Brushes.LightPink;
                ret = SDK.FAIL;
            }
            else
                if (IsColored) TextBox_CfgAvg.Background = Brushes.White;

            // Sections
            if (Byte.TryParse(TextBox_CfgNumSections.Text, out CurConfig.head.num_sections) == false)
            {
                if (IsColored) TextBox_CfgNumSections.Background = Brushes.LightPink;
                ret = SDK.FAIL;
            }
            else
                if (IsColored) TextBox_CfgNumSections.Background = Brushes.White;

            for (Byte i = 0; i < CurConfig.head.num_sections; i++)
            {
                CurConfig.section[i].section_scan_type  = (Byte)(ComboBox_CfgScanType[i].SelectedIndex);
                CurConfig.section[i].width_px           = (Byte)Helper.CfgWidthIndexToPixel(ComboBox_CfgWidth[i].SelectedIndex);
                CurConfig.section[i].exposure_time      = (UInt16)ComboBox_CfgExposure[i].SelectedIndex;

                // Start nm
                if (UInt16.TryParse(TextBox_CfgRangeStart[i].Text, out CurConfig.section[i].wavelength_start_nm) == false ||
                    CurConfig.section[i].wavelength_start_nm < MIN_WAVELENGTH)
                {
                    if (IsColored) TextBox_CfgRangeStart[i].Background = Brushes.LightPink;
                    ret = SDK.FAIL;
                }
                else
                    if (IsColored) TextBox_CfgRangeStart[i].Background = Brushes.White;

                // End nm
                if (UInt16.TryParse(TextBox_CfgRangeEnd[i].Text, out CurConfig.section[i].wavelength_end_nm) == false ||
                    CurConfig.section[i].wavelength_end_nm > MAX_WAVELENGTH)
                {
                    if (IsColored) TextBox_CfgRangeEnd[i].Background = Brushes.LightPink;
                    ret = SDK.FAIL;
                }
                else
                    if (IsColored) TextBox_CfgRangeEnd[i].Background = Brushes.White;

                if (CurConfig.section[i].wavelength_start_nm >= CurConfig.section[i].wavelength_end_nm)
                {
                    if (IsColored) TextBox_CfgRangeStart[i].Background = Brushes.LightPink;
                    if (IsColored) TextBox_CfgRangeEnd[i].Background = Brushes.LightPink;
                    ret = SDK.FAIL;
                }

                // Check Max Patterns
                Int32 MaxPattern = ScanConfig.GetMaxPatterns(CurConfig, i);
                if ((UInt16.TryParse(TextBox_CfgDigRes[i].Text, out CurConfig.section[i].num_patterns) == false) ||
                    (CurConfig.section[i].section_scan_type == 0 && CurConfig.section[i].num_patterns < 2) ||  // Column Mode
                    (CurConfig.section[i].section_scan_type == 1 && CurConfig.section[i].num_patterns < 3) ||  // Hadamard Mode
                    (CurConfig.section[i].num_patterns > MaxPattern) ||
                    (MaxPattern <= 0))
                {
                    if (IsColored) TextBox_CfgDigRes[i].Background = Brushes.LightPink;
                    if (MaxPattern < 0) MaxPattern = 0;
                    ret = SDK.FAIL;
                }
                else
                    if (IsColored) TextBox_CfgDigRes[i].Background = Brushes.White;

                Label_CfgDigRes[i].Content = CurConfig.section[i].num_patterns.ToString() + "/" + MaxPattern.ToString();
                TotalPatterns += CurConfig.section[i].num_patterns;
            }

            Label_CfgNumPatterns.Content = "Total Ptn. Used: " + TotalPatterns.ToString() + "/624";
            if (TotalPatterns > 624)
            {
                String text = "Total number of patterns " + TotalPatterns.ToString() + " exceeds 624!";
                MainWindow.ShowWarning(text);
                ret = SDK.FAIL;
            }

            return ret;
        }

        private Int32 SaveCfgToList(Boolean IsTarget, Boolean IsNew)
        {
            ScanConfig.SlewScanConfig CurConfig = new ScanConfig.SlewScanConfig
            {
                section = new ScanConfig.SlewScanSection[5]
            };

            CurConfig.head.config_name = Helper.CheckRegex(TextBox_CfgName.Text);
            CurConfig.head.scan_type = 2;
            CurConfig.head.num_sections = Byte.Parse(TextBox_CfgNumSections.Text);
            CurConfig.head.num_repeats = UInt16.Parse(TextBox_CfgAvg.Text);

            for (Int32 i = 0; i < CurConfig.head.num_sections; i++)
            {
                CurConfig.section[i].wavelength_start_nm    = UInt16.Parse(TextBox_CfgRangeStart[i].Text);
                CurConfig.section[i].wavelength_end_nm      = UInt16.Parse(TextBox_CfgRangeEnd[i].Text);
                CurConfig.section[i].num_patterns           = UInt16.Parse(TextBox_CfgDigRes[i].Text);
                CurConfig.section[i].section_scan_type      = (Byte)(ComboBox_CfgScanType[i].SelectedIndex);
                CurConfig.section[i].width_px               = (Byte)Helper.CfgWidthIndexToPixel(ComboBox_CfgWidth[i].SelectedIndex);
                CurConfig.section[i].exposure_time          = (UInt16)ComboBox_CfgExposure[i].SelectedIndex;
            }

            if (IsTarget == true && IsNew == true)
            {
                ScanConfig.TargetConfig.Add(CurConfig);
            }
            else if (IsTarget == true && IsNew == false)
            {
                ScanConfig.TargetConfig.RemoveAt(TargetCfg_SelIndex);
                ScanConfig.TargetConfig.Insert(TargetCfg_SelIndex, CurConfig);
            }
            else if (IsTarget == false && IsNew == true)
            {
                LocalConfig.Add(CurConfig);
            }
            else  // if (IsTarget == false && IsNew == false)
            {
                LocalConfig.RemoveAt(LocalCfg_SelIndex);
                LocalConfig.Insert(LocalCfg_SelIndex, CurConfig);
            }

            if (IsTarget == true)
                RefreshTargetCfgList();
            else
                RefreshLocalCfgList();

            return SaveCfgToLocalOrDevice(IsTarget);
        }

        private Int32 SaveCfgToLocalOrDevice(Boolean IsTarget)
        {
            Int32 ret = SDK.FAIL;

            if (IsTarget == true)
            {
                if ((ret = ScanConfig.SetConfigList()) == 0)
                    MainWindow.ShowInfo("Device Config List Update Success!");
                else
                    MainWindow.ShowError("Device Config List Update Failed!");
            }
            else
            {
                String FileName = Path.Combine(MainWindow.ConfigDir, "ConfigList.xml");
                XmlSerializer xml = new XmlSerializer(typeof(List<ScanConfig.SlewScanConfig>));
                TextWriter writer = new StreamWriter(FileName);
                xml.Serialize(writer, LocalConfig);
                writer.Close();
                MainWindow.ShowInfo("Local Config List Update Success!");
                ret = SDK.PASS;
            }

            return ret;
        }

        private void Button_CopyCfgL2T_Click(object sender, RoutedEventArgs e)
        {
            if (LocalCfg_SelIndex < 0)
            {
                MainWindow.ShowWarning("No item selected.");
                return;
            }

            if (ScanConfig.TargetConfig.Count >= 20)
            {
                MainWindow.ShowWarning("Number of scan configs in device cannot exceed 20.");
                return;
            }

            ScanConfig.TargetConfig.Add(LocalConfig[LocalCfg_SelIndex]);
            RefreshTargetCfgList();
            SaveCfgToLocalOrDevice(true);
        }

        private void Button_CopyCfgT2L_Click(object sender, RoutedEventArgs e)
        {
            if (TargetCfg_SelIndex < 0)
            {
                MainWindow.ShowWarning("No item selected.");
                return;
            }

            LocalConfig.Add(ScanConfig.TargetConfig[TargetCfg_SelIndex]);
            RefreshLocalCfgList();
            SaveCfgToLocalOrDevice(false);
        }

        private void Button_MoveCfgL2T_Click(object sender, RoutedEventArgs e)
        {
            if (LocalCfg_SelIndex < 0)
            {
                MainWindow.ShowWarning("No item selected.");
                return;
            }

            if (ScanConfig.TargetConfig.Count >= 20)
            {
                MainWindow.ShowWarning("Number of scan configs in device cannot exceed 20.");
                return;
            }

            if (DevCurCfg_Index == LocalCfg_SelIndex)
            {
                MainWindow.ShowWarning("Device current configuration will be moved,\n" +
                                       "please set a new one to device later,\n" +
                                       "or you can not do scan.");

                // Clear previous scan data
                ClearScanPlotsUI();

                Button_Scan.IsEnabled = false;
            }

            ScanConfig.TargetConfig.Add(LocalConfig[LocalCfg_SelIndex]);
            LocalConfig.RemoveAt(LocalCfg_SelIndex);
            RefreshLocalCfgList();
            SaveCfgToLocalOrDevice(false);
            RefreshTargetCfgList();
            ListBox_TargetCfgs.SelectedIndex = ScanConfig.TargetConfig.Count - 1;
            SaveCfgToLocalOrDevice(true);
        }

        private void Button_MoveCfgT2L_Click(object sender, RoutedEventArgs e)
        {
            if (TargetCfg_SelIndex < 0)
            {
                MainWindow.ShowWarning("No item selected.");
                return;
            }

            if (TargetCfg_SelIndex == 0)
            {
                MainWindow.ShowWarning("The built-in default configuration cannot be moved.");
                return;
            }

            if (DevCurCfg_Index == TargetCfg_SelIndex)
            {
                MainWindow.ShowWarning("Device current configuration will be moved,\n" +
                                       "please set a new one to device later,\n" +
                                       "or you can not do scan.");

                // Clear previous scan data
                ClearScanPlotsUI();

                Button_Scan.IsEnabled = false;
            }

            Int32 ActiveIndex = ScanConfig.GetTargetActiveScanIndex();

            LocalConfig.Add(ScanConfig.TargetConfig[TargetCfg_SelIndex]);
            ScanConfig.TargetConfig.RemoveAt(TargetCfg_SelIndex);
            if (TargetCfg_SelIndex == ActiveIndex)
                ActiveIndex = 0;
            else if (TargetCfg_SelIndex < ActiveIndex)
                ActiveIndex--;
            ScanConfig.SetTargetActiveScanIndex(ActiveIndex);

            RefreshLocalCfgList();
            ListBox_LocalCfgs.SelectedIndex = LocalConfig.Count - 1;
            SaveCfgToLocalOrDevice(false);
            RefreshTargetCfgList();
            SaveCfgToLocalOrDevice(true);
        }

        private void ListBox_LocalCfgs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GroupBox_CfgDetails.IsEnabled = false;
            Button_CfgSave.IsEnabled = false;
            Button_CfgCancel.IsEnabled = false;

            LocalCfg_SelIndex = ListBox_LocalCfgs.SelectedIndex;
            if (LocalCfg_SelIndex < 0 || LocalConfig.Count == 0)
                return;
            else
            {
                if (!Scan.IsLocalRefExist && ReferenceSelect != Scan.SCAN_REF_TYPE.SCAN_REF_BUILT_IN)
                {
                    RadioButton_RefPre.IsChecked = false;
                    RadioButton_RefNew.IsChecked = true;
                }
            }

            SelCfg_IsTarget = false;
            FillCfgDetailsContent();

            // Clear target listbox index after local config data refreshed.
            if (ListBox_TargetCfgs.SelectedIndex != -1)
                ListBox_TargetCfgs.SelectedIndex = -1;
        }

        private void ListBox_TargetCfgs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GroupBox_CfgDetails.IsEnabled = false;
            Button_CfgSave.IsEnabled = false;
            Button_CfgCancel.IsEnabled = false;

            TargetCfg_SelIndex = ListBox_TargetCfgs.SelectedIndex;
            if (TargetCfg_SelIndex < 0 || ScanConfig.TargetConfig.Count == 0)
                return;
            else
            {
                if (!Scan.IsLocalRefExist && ReferenceSelect != Scan.SCAN_REF_TYPE.SCAN_REF_BUILT_IN)
                {
                    RadioButton_RefPre.IsChecked = false;
                    RadioButton_RefNew.IsChecked = true;
                }
            }

            SelCfg_IsTarget = true;
            FillCfgDetailsContent();

            // Clear local listbox index after target config data refreshed.
            if (ListBox_LocalCfgs.SelectedIndex != -1)
                ListBox_LocalCfgs.SelectedIndex = -1;
        }

        private void Button_TargetCfgSetActive_Click(object sender, RoutedEventArgs e)
        {
            if (TargetCfg_SelIndex < 0)
            {
                MainWindow.ShowWarning("No item selected.");
                return;
            }
            ScanConfig.SetTargetActiveScanIndex(TargetCfg_SelIndex);
            SetScanConfig(ScanConfig.TargetConfig[TargetCfg_SelIndex], true, TargetCfg_SelIndex);
            RefreshTargetCfgList();
        }

        private void CfgDetails_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshCfgSectionItems();
            IsCfgLegal(false);
        }

        private void CfgDetails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsCfgLegal(false);
        }

        private void Button_CfgNew_Click(object sender, RoutedEventArgs e)
        {
            // Clear listbox index before someone set focus.
            if (ListBox_LocalCfgs.SelectedIndex != -1)
                ListBox_LocalCfgs.SelectedIndex = -1;
            if (ListBox_TargetCfgs.SelectedIndex != -1)
                ListBox_TargetCfgs.SelectedIndex = -1;

            InitCfgDetailsContent();

            GroupBox_CfgDetails.IsEnabled = true;
            Grid_CfgSection1.IsEnabled = false;
            Grid_CfgSection2.IsEnabled = false;
            Grid_CfgSection3.IsEnabled = false;
            Grid_CfgSection4.IsEnabled = false;
            Grid_CfgSection5.IsEnabled = false;
            Button_CfgSave.IsEnabled = true;
            Button_CfgCancel.IsEnabled = true;

            SelCfg_IsNew = true;
            TextBox_CfgName.Focus();
        }

        private void Button_CfgEdit_Click(object sender, RoutedEventArgs e)
        {
            InitCfgDetailsBgColor();

            if (SelCfg_IsTarget == true)
            {
                if (TargetCfg_SelIndex < 0)
                {
                    MainWindow.ShowWarning("No item selected.");
                    return;
                }
            }
            else
            {
                if (LocalCfg_SelIndex < 0)
                {
                    MainWindow.ShowWarning("No item selected.");
                    return;
                }
            }

            GroupBox_CfgDetails.IsEnabled = true;
            Button_CfgSave.IsEnabled = true;
            Button_CfgCancel.IsEnabled = true;
            RefreshCfgSectionItems();

            SelCfg_IsNew = false;
            TextBox_CfgName.Focus();
        }

        private void Button_CfgDelete_Click(object sender, RoutedEventArgs e)
        {
            GroupBox_CfgDetails.IsEnabled = false;
            Int32 ActiveIndex = ScanConfig.GetTargetActiveScanIndex();

            if (SelCfg_IsTarget == true)
            {
                if (TargetCfg_SelIndex < 0)
                {
                    MainWindow.ShowWarning("No item selected.");
                    return;
                }
                else if (TargetCfg_SelIndex == 0)
                {
                    MainWindow.ShowWarning("The built-in default configuration cannot be deleted.");
                    return;
                }
            }
            else
            {
                if (LocalCfg_SelIndex < 0)
                {
                    MainWindow.ShowWarning("No item selected.");
                    return;
                }
            }

            MessageBoxResult Result = MainWindow.ShowQuestion("Are you sure to delete this configuration?", MessageBoxButton.YesNo);
            switch (Result)
            {
                case MessageBoxResult.Yes:
                    if (DevCurCfg_IsTarget == SelCfg_IsTarget)
                    {
                        if (DevCurCfg_Index == TargetCfg_SelIndex || DevCurCfg_Index == LocalCfg_SelIndex)
                        {
                            MainWindow.ShowWarning("Device current configuration will be deleted,\n" +
                                                   "please set a new one to device later,\n" +
                                                   "or you can not do scan.");

                            // Clear previous scan data
                            ClearScanPlotsUI();

                            Button_Scan.IsEnabled = false;
                        }
                    }

                    if (SelCfg_IsTarget == true)
                    {
                        ScanConfig.TargetConfig.RemoveAt(TargetCfg_SelIndex);
                        if (TargetCfg_SelIndex == ActiveIndex)
                            ActiveIndex = 0;
                        else if (TargetCfg_SelIndex < ActiveIndex)
                            ActiveIndex--;
                        ScanConfig.SetTargetActiveScanIndex(ActiveIndex);

                        RefreshTargetCfgList();
                        SaveCfgToLocalOrDevice(true);
                        DBG.WriteLine("Delete this Device configuration");
                    }
                    else
                    {
                        LocalConfig.RemoveAt(LocalCfg_SelIndex);
                        RefreshLocalCfgList();
                        SaveCfgToLocalOrDevice(false);
                        DBG.WriteLine("Delete this Local configuration");
                    }
                    InitCfgDetailsContent();
                    break;
                default:
                case MessageBoxResult.No:
                    DBG.WriteLine("Cancel deleting");
                    break;
            }
        }

        private void Button_CfgSave_Click(object sender, RoutedEventArgs e)
        {
            if (IsCfgLegal(true) == SDK.FAIL)
            {
                MainWindow.ShowError("Error configuration data can't be saved!");
                return;
            }

            String Text;
            MessageBoxResult Result;

            GroupBox_CfgDetails.IsEnabled = false;

            if (SelCfg_IsNew == true)
            {
                Text = "Do you want to save this configuration to Device?\n\n" +
                       "Yes,\t save to Device;\n" +
                       "No,\t save to Local;\n" +
                       "Cancel,\t not to save now.";
                Result = MainWindow.ShowQuestion(Text, MessageBoxButton.YesNoCancel);

                switch (Result)
                {
                    case MessageBoxResult.Yes:
                        SaveCfgToList(true, true);  // Save new to target
                        DBG.WriteLine("Save new config to Device");
                        break;
                    case MessageBoxResult.No:
                        SaveCfgToList(false, true);  // Save new to local
                        DBG.WriteLine("Save new config to Local");
                        break;
                    default:
                    case MessageBoxResult.Cancel:
                        GroupBox_CfgDetails.IsEnabled = true;
                        DBG.WriteLine("Cancel saving");
                        break;
                }
            }
            else
            {
                Int32 ret = SDK.FAIL;

                Text = "Finish editing?";
                Result = MainWindow.ShowQuestion(Text, MessageBoxButton.YesNo);

                switch (Result)
                {
                    case MessageBoxResult.Yes:
                    {
                        int CurIndex;
                        ListBoxItem item;

                        if (SelCfg_IsTarget == true)
                        {
                            CurIndex = TargetCfg_SelIndex;  // Record index before refresh config list
                            ret = SaveCfgToList(true, false);  // Save editing to target

                            ListBox_TargetCfgs.SelectedIndex = CurIndex;
                            ListBox_TargetCfgs.UpdateLayout();  // Pre-generates item containers
                            item = (ListBoxItem)ListBox_TargetCfgs.ItemContainerGenerator.ContainerFromIndex(ListBox_TargetCfgs.SelectedIndex);
                              
                            DBG.WriteLine("Save editing config to Device");
                        }
                        else
                        {
                            CurIndex = LocalCfg_SelIndex;  // Record index before refresh config list
                            ret = SaveCfgToList(false, false);  // Save editing to local

                            ListBox_LocalCfgs.SelectedIndex = CurIndex;
                            ListBox_LocalCfgs.UpdateLayout();  // Pre-generates item containers
                            item = (ListBoxItem)ListBox_LocalCfgs.ItemContainerGenerator.ContainerFromIndex(ListBox_LocalCfgs.SelectedIndex);

                            DBG.WriteLine("Save editing config to Local");
                        }
                        item.Focus();

                        if (DevCurCfg_IsTarget == SelCfg_IsTarget)
                        {
                            if (ret == SDK.PASS)
                            {
                                if (DevCurCfg_Index == TargetCfg_SelIndex)
                                {
                                    SetScanConfig(ScanConfig.TargetConfig[TargetCfg_SelIndex], true, TargetCfg_SelIndex);
                                    scan_Params.ScanAvg = ScanConfig.TargetConfig[TargetCfg_SelIndex].head.num_repeats;
                                }
                                   
                                else if (DevCurCfg_Index == LocalCfg_SelIndex)
                                {
                                    SetScanConfig(LocalConfig[LocalCfg_SelIndex], false, LocalCfg_SelIndex);
                                    scan_Params.ScanAvg = LocalConfig[LocalCfg_SelIndex].head.num_repeats;
                                }                                   
                            }
                            else
                            {
                                MainWindow.ShowWarning("Please set a new config to device later,\n" +
                                                       "or you can not do scan.");
                                
                                // Clear previous scan data
                                ClearScanPlotsUI();

                                Button_Scan.IsEnabled = false;
                            }
                        }
                        break;
                    }
                    default:
                    case MessageBoxResult.No:
                        GroupBox_CfgDetails.IsEnabled = true;
                        DBG.WriteLine("Cancel saving");
                        break;
                }
            }
        }

        private void Button_CfgCancel_Click(object sender, RoutedEventArgs e)
        {
            if (SelCfg_IsNew == true)  // Only clear config details content
            {
                InitCfgDetailsContent();
            }
            else  // Recover the selected config focus and data if the index is still exist
            {
                ListBoxItem item;
                if (SelCfg_IsTarget == true)
                {
                    ListBox_TargetCfgs.SelectedIndex = TargetCfg_SelIndex;
                    ListBox_TargetCfgs.UpdateLayout();  // Pre-generates item containers
                    item = (ListBoxItem)ListBox_TargetCfgs.ItemContainerGenerator.ContainerFromIndex(ListBox_TargetCfgs.SelectedIndex);
                }
                else
                {
                    ListBox_LocalCfgs.SelectedIndex = LocalCfg_SelIndex;
                    ListBox_LocalCfgs.UpdateLayout();  // Pre-generates item containers
                    item = (ListBoxItem)ListBox_LocalCfgs.ItemContainerGenerator.ContainerFromIndex(ListBox_LocalCfgs.SelectedIndex);
                }
                item.Focus();
                FillCfgDetailsContent();
            }

            GroupBox_CfgDetails.IsEnabled = false;
            Button_CfgSave.IsEnabled = false;
            Button_CfgCancel.IsEnabled = false;
        }

        #endregion

        #region Save Scanned Files

        private void SaveHeader(FileStream fs, out StreamWriter sw, Boolean ifJCAMP)
        {
            sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            String TmpStrScan = String.Empty, TmpStrRef = String.Empty, PreStr = String.Empty;
            UInt16 TotalScanPtns = 0, TotalRefPtns = 0;

            String ModelName = Device.DevInfo.ModelName;
            String TivaRev = Device.DevInfo.TivaRev[0].ToString() + "."
                           + Device.DevInfo.TivaRev[1].ToString() + "."
                           + Device.DevInfo.TivaRev[2].ToString() + "."
                           + Device.DevInfo.TivaRev[3].ToString();
            String DLPCRev = Device.DevInfo.DLPCRev[0].ToString() + "."
                           + Device.DevInfo.DLPCRev[1].ToString() + "."
                           + Device.DevInfo.DLPCRev[2].ToString();
            String UUID = BitConverter.ToString(Device.DevInfo.DeviceUUID).Replace("-", ":");
            String HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;

            if (ifJCAMP == true)
            {
                PreStr = "##";

                sw.WriteLine("##TITLE=" + Scan.ScanConfigData.head.config_name);
                sw.WriteLine("##JCAMP-DX=4.24");
                sw.WriteLine("##DATA TYPE=INFRARED SPECTRUM");
            }
            else
            {
                PreStr = String.Empty;
            }

            TmpStrScan = Scan.ScanConfigData.head.config_name;
            TmpStrRef = (RadioButton_RefFac.IsChecked == true) ? "Built-In Reference" : "User Reference";
            sw.WriteLine(PreStr + "Method:," + TmpStrScan + "," + TmpStrRef + ",,,Model Name," + ModelName);

            TmpStrScan = Scan.ScanDateTime[2] + "/" + Scan.ScanDateTime[1] + "/" + Scan.ScanDateTime[0] + " @ " +
                         Scan.ScanDateTime[3] + ":" + Scan.ScanDateTime[4] + ":" + Scan.ScanDateTime[5];
            TmpStrRef = Scan.ReferenceScanDateTime[2] + "/" + Scan.ReferenceScanDateTime[1] + "/" + Scan.ReferenceScanDateTime[0] + " @ " +
                        Scan.ReferenceScanDateTime[3] + ":" + Scan.ReferenceScanDateTime[4] + ":" + Scan.ReferenceScanDateTime[5];
            sw.WriteLine(PreStr + "Host Date-Time:," + TmpStrScan + "," + TmpStrRef + ",,,GUI Version," + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());

            sw.WriteLine(PreStr + "Header Version:," + Scan.ScanDataVersion + "," + Scan.ReferenceScanDataVersion + ",,,Tiva Version," + TivaRev);

            sw.WriteLine(PreStr + "System Temp (C):," + Scan.SensorData[0] + "," + Scan.ReferenceSensorData[0] + ",,,DLPC Version," + DLPCRev);

            sw.WriteLine(PreStr + "Detector Temp (C)," + Scan.SensorData[1] + "," + Scan.ReferenceSensorData[1] + ",,,UUID," + UUID);

            sw.WriteLine(PreStr + "Humidity (%):," + Scan.SensorData[2] + "," + Scan.ReferenceSensorData[2] + ",,,Main Board Version," + HWRev);

            sw.WriteLine(PreStr + "Lamp PD:," + Scan.SensorData[3] + "," + Scan.ReferenceSensorData[3]);

            sw.WriteLine(PreStr + "Shift Vector Coefficients:," + Device.Calib_Coeffs.ShiftVectorCoeffs[0] + "," +
                                                                  Device.Calib_Coeffs.ShiftVectorCoeffs[1] + "," +
                                                                  Device.Calib_Coeffs.ShiftVectorCoeffs[2]);

            sw.WriteLine(PreStr + "Pixel to Wavelength Coefficients:," + Device.Calib_Coeffs.PixelToWavelengthCoeffs[0] + "," +
                                                                         Device.Calib_Coeffs.PixelToWavelengthCoeffs[1] + "," +
                                                                         Device.Calib_Coeffs.PixelToWavelengthCoeffs[2]);

            sw.WriteLine(PreStr + "Serial Number:," + Scan.ScanConfigData.head.ScanConfig_serial_number + "," +
                                                      Scan.ReferenceScanConfigData.head.ScanConfig_serial_number);

            sw.WriteLine(PreStr + "Scan Config Name:," + Scan.ScanConfigData.head.config_name + "," +
                                                         Scan.ReferenceScanConfigData.head.config_name);

            if (Scan.ScanConfigData.head.num_sections == 1)
            {
                TmpStrScan = Helper.ScanTypeIndexToMode(Scan.ScanConfigData.section[0].section_scan_type);
                TmpStrRef = Helper.ScanTypeIndexToMode(Scan.ReferenceScanConfigData.section[0].section_scan_type);
            }
            else
            {
                TmpStrScan = Helper.ScanTypeIndexToMode(Scan.ScanConfigData.head.scan_type);
                TmpStrRef = Helper.ScanTypeIndexToMode(Scan.ReferenceScanConfigData.head.scan_type);
            }
            sw.WriteLine(PreStr + "Scan Config Type:," + TmpStrScan + "," + TmpStrRef);

            for (Int32 i = 0; i < Scan.ScanConfigData.head.num_sections; i++)
            {
                if (Scan.ScanConfigData.head.num_sections > 1)
                {
                    TmpStrScan = Helper.ScanTypeIndexToMode(Scan.ScanConfigData.section[i].section_scan_type);
                    TmpStrRef = (Scan.ReferenceScanConfigData.head.num_sections > 1 || i == 0) ? Helper.ScanTypeIndexToMode(Scan.ReferenceScanConfigData.section[i].section_scan_type) : String.Empty;
                    sw.WriteLine(PreStr + "Section " + (i + 1) + "," + TmpStrScan + "," + TmpStrRef);
                }

                TmpStrScan = Scan.ScanConfigData.section[i].wavelength_start_nm.ToString();
                TmpStrRef = (Scan.ReferenceScanConfigData.head.num_sections > 1 || i == 0) ? Scan.ReferenceScanConfigData.section[i].wavelength_start_nm.ToString() : String.Empty;
                sw.WriteLine(PreStr + "Start wavelength (nm):," + TmpStrScan + "," + TmpStrRef);

                TmpStrScan = Scan.ScanConfigData.section[i].wavelength_end_nm.ToString();
                TmpStrRef = (Scan.ReferenceScanConfigData.head.num_sections > 1 || i == 0) ? Scan.ReferenceScanConfigData.section[i].wavelength_end_nm.ToString() : String.Empty;
                sw.WriteLine(PreStr + "End wavelength (nm):," + TmpStrScan + "," + TmpStrRef);

                TmpStrScan = Math.Round(Helper.CfgWidthPixelToNM(Scan.ScanConfigData.section[i].width_px), 2).ToString();
                TmpStrRef = (Scan.ReferenceScanConfigData.head.num_sections > 1 || i == 0) ? Math.Round(Helper.CfgWidthPixelToNM(Scan.ReferenceScanConfigData.section[i].width_px), 2).ToString() : String.Empty;
                sw.WriteLine(PreStr + "Pattern Pixel Width (nm):," + TmpStrScan + "," + TmpStrRef);

                TmpStrScan = Helper.CfgExpIndexToTime(Scan.ScanConfigData.section[i].exposure_time).ToString();
                TmpStrRef = (Scan.ReferenceScanConfigData.head.num_sections > 1 || i == 0) ? Helper.CfgExpIndexToTime(Scan.ReferenceScanConfigData.section[i].exposure_time).ToString() : String.Empty;
                sw.WriteLine(PreStr + "Exposure (ms):," + TmpStrScan + "," + TmpStrRef);

                TmpStrScan = Scan.ScanConfigData.section[i].num_patterns.ToString();
                TmpStrRef = (Scan.ReferenceScanConfigData.head.num_sections > 1 || i == 0) ? Scan.ReferenceScanConfigData.section[i].num_patterns.ToString() : String.Empty;
                sw.WriteLine(PreStr + "Digital Resolution:," + TmpStrScan + "," + TmpStrRef);
            }

            for (Int32 i = 0; i < Scan.ScanConfigData.head.num_sections; i++)
            {
                TotalScanPtns += Scan.ScanConfigData.section[i].num_patterns;
            }
            for (Int32 i = 0; i < Scan.ReferenceScanConfigData.head.num_sections; i++)
            {
                TotalRefPtns += Scan.ReferenceScanConfigData.section[i].num_patterns;
            }
            if (ifJCAMP == true)
            {
                sw.WriteLine("##NPOINTS=" + TotalScanPtns);
            }
            else
            {
                sw.WriteLine("Total Digital Resolution:," + TotalScanPtns + "," + TotalRefPtns);
            }

            sw.WriteLine(PreStr + "Num Repeats:," + Scan.ScanConfigData.head.num_repeats + "," + Scan.ReferenceScanConfigData.head.num_repeats);

            sw.WriteLine(PreStr + "PGA Gain:," + Scan.PGA + "," + Scan.ReferencePGA);

            TimeSpan ts = new TimeSpan(TimeScanEnd.Ticks - TimeScanStart.Ticks);
            sw.WriteLine(PreStr + "Total Measurement Time in sec:," + ts.TotalSeconds);
        }
        private void SaveHeader_CSV(StreamWriter sw)
        {
            String ModelName = Device.DevInfo.ModelName;
            String TivaRev = Device.DevInfo.TivaRev[0].ToString() + "."
                           + Device.DevInfo.TivaRev[1].ToString() + "."
                           + Device.DevInfo.TivaRev[2].ToString() + "."
                           + Device.DevInfo.TivaRev[3].ToString();
            String DLPCRev = Device.DevInfo.DLPCRev[0].ToString() + "."
                           + Device.DevInfo.DLPCRev[1].ToString() + "."
                           + Device.DevInfo.DLPCRev[2].ToString();
            String SpecLibRev = Device.DevInfo.SpecLibRev[0].ToString() + "."
                           + Device.DevInfo.SpecLibRev[1].ToString() + "."
                           + Device.DevInfo.SpecLibRev[2].ToString();
            String UUID = BitConverter.ToString(Device.DevInfo.DeviceUUID).Replace("-", ":");
            String MB_HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;
            String DB_HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(2, 1) : String.Empty;
            String Manufacturing_SerNum = Device.DevInfo.Manufacturing_SerialNumber;
            //--------------------------------------------------------
            String Data_Date_Time = String.Empty, Ref_Config_Name = String.Empty, Ref_Data_Date_Time = String.Empty;
            String Section_Config_Type = String.Empty, Ref_Section_Config_Type = String.Empty;
            String Pattern_Width = String.Empty, Ref_Pattern_Width = String.Empty;
            String Exposure = String.Empty, Ref_Exposure = String.Empty;
            //----------------------------------------------

            String[,] CSV = new String[29, 15];
            for (int i = 0; i < 29; i++)
                for (int j = 0; j < 15; j++)
                    CSV[i, j] = ",";

            // Section information field names
            CSV[0, 0] = "***Scan Config Information***,";
            CSV[0, 7] = "***Reference Scan Information***";
            CSV[17, 0] = "***General Information***,";
            CSV[17, 7] = "***Calibration Coefficients***";
            CSV[28, 0] = "***Scan Data***";
            // Config field names & values
            for (int i = 0; i < 2; i++)
            {
                CSV[1, i * 7] = "Scan Config Name:,";
                CSV[2, i * 7] = "Scan Config Type:,";
                CSV[2, i * 7 + 2] = "Num Section:,";
                CSV[3, i * 7] = "Section Config Type:,";
                CSV[4, i * 7] = "Start Wavelength (nm):,";
                CSV[5, i * 7] = "End Wavelength (nm):,";
                CSV[6, i * 7] = "Pattern Width (nm):,";
                CSV[7, i * 7] = "Exposure (ms):,";
                CSV[8, i * 7] = "Digital Resolution:,";
                CSV[9, i * 7] = "Num Repeats:,";
                CSV[10, i * 7] = "PGA Gain:,";
                CSV[11, i * 7] = "System Temp (C):,";
                CSV[12, i * 7] = "Humidity (%):,";
                CSV[13, i * 7] = "Lamp PT:,";
                CSV[14, i * 7] = "Data Date-Time:,";
            }
            for (int i = 0; i < Scan.ScanConfigData.head.num_sections; i++)
            {
                if (i == 0)
                {
                    // Scan config values
                    CSV[1, 1] = Scan.ScanConfigData.head.config_name + ",";
                    CSV[2, 1] = "Slew,";
                    CSV[2, 3] = Scan.ScanConfigData.head.num_sections + ",";
                    CSV[9, 1] = Scan.ScanConfigData.head.num_repeats + ",";
                    CSV[10, 1] = Scan.PGA + ",";
                    CSV[11, 1] = Scan.SensorData[0] + ",";
                    CSV[12, 1] = Scan.SensorData[2] + ",";
                    CSV[13, 1] = Scan.SensorData[3] + ",";

                    Data_Date_Time = Scan.ScanDateTime[2] + "/" + Scan.ScanDateTime[1] + "/" + Scan.ScanDateTime[0] + " @ " +
                                 Scan.ScanDateTime[3] + ":" + Scan.ScanDateTime[4] + ":" + Scan.ScanDateTime[5];
                    CSV[14, 1] = Data_Date_Time + ",";

                    //Reference config values
                    Ref_Config_Name = (RadioButton_RefFac.IsChecked == true) ? "Built-In Reference" : "User Reference";
                    if (RadioButton_RefFac.IsChecked == true)
                    {
                        if (Scan.ReferenceScanConfigData.head.config_name == "SystemTest")
                        {
                            Ref_Config_Name = "Built-in Factory Reference,";
                        }
                        else
                        {
                            Ref_Config_Name = "Built-in User Reference,";
                        }
                    }
                    else
                    {
                        Ref_Config_Name = "Local New Reference,";
                    }
                    CSV[1, 8] = Ref_Config_Name + ",";
                    CSV[2, 8] = "Slew,";
                    CSV[2, 10] = Scan.ReferenceScanConfigData.head.num_sections + ",";
                    CSV[9, 8] = Scan.ReferenceScanConfigData.head.num_repeats + ",";
                    CSV[10, 8] = Scan.ReferencePGA + ",";
                    CSV[11, 8] = Scan.ReferenceSensorData[0] + ",";
                    CSV[12, 8] = Scan.ReferenceSensorData[2] + ",";
                    CSV[13, 8] = Scan.ReferenceSensorData[3] + ",";

                    Ref_Data_Date_Time = Scan.ReferenceScanDateTime[2] + "/" + Scan.ReferenceScanDateTime[1] + "/" + Scan.ReferenceScanDateTime[0] + " @ " +
                           Scan.ReferenceScanDateTime[3] + ":" + Scan.ReferenceScanDateTime[4] + ":" + Scan.ReferenceScanDateTime[5];
                    CSV[14, 8] = Ref_Data_Date_Time + ",";
                }
                // Scan config section values
                Section_Config_Type = Helper.ScanTypeIndexToMode(Scan.ScanConfigData.section[i].section_scan_type);
                CSV[3, i + 1] = Section_Config_Type + ",";
                CSV[4, i + 1] = Scan.ScanConfigData.section[i].wavelength_start_nm.ToString() + ",";
                CSV[5, i + 1] = Scan.ScanConfigData.section[i].wavelength_end_nm.ToString() + ",";

                Pattern_Width = Math.Round(Helper.CfgWidthPixelToNM(Scan.ScanConfigData.section[i].width_px), 2).ToString();
                CSV[6, i + 1] = Pattern_Width + ",";

                Exposure = Helper.CfgExpIndexToTime(Scan.ScanConfigData.section[i].exposure_time).ToString();
                CSV[7, i + 1] = Exposure + ",";
                CSV[8, i + 1] = Scan.ScanConfigData.section[i].num_patterns.ToString() + ",";

                // Reference config section values
                if (i < Scan.ReferenceScanConfigData.head.num_sections)
                {
                    Ref_Section_Config_Type = Helper.ScanTypeIndexToMode(Scan.ReferenceScanConfigData.section[i].section_scan_type);
                    CSV[3, i + 8] = Ref_Section_Config_Type + ",";

                    CSV[4, i + 8] = Scan.ReferenceScanConfigData.section[i].wavelength_start_nm.ToString() + ",";
                    CSV[5, i + 8] = Scan.ReferenceScanConfigData.section[i].wavelength_end_nm.ToString() + ",";

                    Ref_Pattern_Width = (Scan.ReferenceScanConfigData.head.num_sections > 1 || i == 0) ? Math.Round(Helper.CfgWidthPixelToNM(Scan.ReferenceScanConfigData.section[i].width_px), 2).ToString() : String.Empty;
                    CSV[6, i + 8] = Ref_Pattern_Width + ",";

                    Ref_Exposure = (Scan.ReferenceScanConfigData.head.num_sections > 1 || i == 0) ? Helper.CfgExpIndexToTime(Scan.ReferenceScanConfigData.section[i].exposure_time).ToString() : String.Empty;
                    CSV[7, i + 8] = Ref_Exposure + ",";
                    CSV[8, i + 8] = Scan.ReferenceScanConfigData.section[i].num_patterns.ToString() + ",";
                }
            }

            // Measure Time field name & value
            CSV[15, 0] = "Total Measurement Time in sec:,";
            TimeSpan ts = new TimeSpan(TimeScanEnd.Ticks - TimeScanStart.Ticks);
            CSV[15, 1] = ts.TotalSeconds.ToString() + ",";

            // Coefficients filed names & valus
            CSV[18, 7] = "Shift Vector Coefficients:,";
            CSV[18, 8] = Device.Calib_Coeffs.ShiftVectorCoeffs[0].ToString() + ",";
            CSV[18, 9] = Device.Calib_Coeffs.ShiftVectorCoeffs[1].ToString() + ",";
            CSV[18, 10] = Device.Calib_Coeffs.ShiftVectorCoeffs[2].ToString() + ",";
            CSV[19, 7] = "Pixel to Wavelength Coefficients:,";
            CSV[19, 8] = Device.Calib_Coeffs.PixelToWavelengthCoeffs[0].ToString() + ",";
            CSV[19, 9] = Device.Calib_Coeffs.PixelToWavelengthCoeffs[1].ToString() + ",";
            CSV[19, 10] = Device.Calib_Coeffs.PixelToWavelengthCoeffs[2].ToString() + ",";

            // General information field names & values
            CSV[18, 0] = "Model Name:,";
            CSV[18, 1] = ModelName + ",";
            CSV[19, 0] = "Serial Number:,";
            CSV[19, 1] = Scan.ScanConfigData.head.ScanConfig_serial_number + ",";
            CSV[19, 2] = "(" + Manufacturing_SerNum + "),";
            CSV[20, 0] = "GUI Version:,";
            CSV[20, 1] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + ",";
            CSV[21, 0] = "TIVA Version:,";
            CSV[21, 1] = TivaRev + ",";
            CSV[22, 0] = "DLPC Version:,";
            CSV[22, 1] = DLPCRev + ",";
            CSV[23, 0] = "DLPSPECLIB Version:,";
            CSV[23, 1] = SpecLibRev + ",";
            CSV[24, 0] = "UUID:,";
            CSV[24, 1] = UUID + ",";
            CSV[25, 0] = "Main Board Version:,";
            CSV[26, 0] = "Detector Board Version:,";
            CSV[25, 1] = MB_HWRev + ",";
            CSV[26, 1] = DB_HWRev + ",";

            string buf = "";
            for (int i = 0; i < 29; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    buf += CSV[i, j];
                    if (j == 14)
                    {
                        sw.WriteLine(buf);
                    }
                }
                buf = "";
            }
        }

        private void SaveToCSV(String FileName)
        {
            if (CheckBox_SaveCombCSV.IsChecked == true)
            {
                FileStream fs = new FileStream(FileName, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
                //SaveHeader(in fs, out StreamWriter sw, false);
                SaveHeader_CSV(sw);

                sw.WriteLine("Wavelength (nm),Absorbance (AU),Reference Signal (unitless),Sample Signal (unitless)");
                for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                {
                    sw.WriteLine(Scan.WaveLength[i] + "," + Scan.Absorbance[i] + "," + Scan.ReferenceIntensity[i] + "," + Scan.Intensity[i]);
                }

                sw.Flush();  // Clear buffer
                sw.Close();  // Close file stream
            }

            if (CheckBox_SaveICSV.IsChecked == true)
            {
                String FileName_i = FileName.Insert(FileName.LastIndexOf("_", FileName.Length - 20), "_i");
                FileStream fs = new FileStream(FileName_i, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
                //SaveHeader(in fs, out StreamWriter sw, false);
                SaveHeader_CSV(sw);

                sw.WriteLine("Wavelength (nm),Sample Signal (unitless)");
                for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                {
                    sw.WriteLine(Scan.WaveLength[i] +  "," + Scan.Intensity[i]);
                }

                sw.Flush();  // Clear buffer
                sw.Close();  // Close file stream
            }

            if (CheckBox_SaveACSV.IsChecked == true)
            {
                String FileName_a = FileName.Insert(FileName.LastIndexOf("_", FileName.Length - 20), "_a");
                FileStream fs = new FileStream(FileName_a, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
                //SaveHeader(in fs, out StreamWriter sw, false);
                SaveHeader_CSV(sw);

                sw.WriteLine("Wavelength (nm),Absorbance (AU)");
                for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                {
                    sw.WriteLine(Scan.WaveLength[i] + "," + Scan.Absorbance[i]);
                }

                sw.Flush();  // Clear buffer
                sw.Close();  // Close file stream
            }

            if (CheckBox_SaveRCSV.IsChecked == true)
            {
                String FileName_r = FileName.Insert(FileName.LastIndexOf("_", FileName.Length - 20), "_r");
                FileStream fs = new FileStream(FileName_r, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
                //SaveHeader(in fs, out StreamWriter sw, false);
                SaveHeader_CSV(sw);

                sw.WriteLine("Wavelength (nm),Reflectance (AU)");
                for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                {
                    sw.WriteLine(Scan.WaveLength[i] + "," + Scan.Reflectance[i]);
                }

                sw.Flush();  // Clear buffer
                sw.Close();  // Close file stream
            }

            if (CheckBox_SaveOneCSV.IsChecked == true)
            {
                if (CheckBox_SaveOneCSV.IsChecked == true && OneScanFileName == String.Empty)
                    OneScanFileName = FileName;

                String FileName_one = OneScanFileName.Insert(OneScanFileName.LastIndexOf("_", OneScanFileName.Length - 20), "_one");

                using (FileStream fs = new FileStream(FileName_one, FileMode.Append, FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                    {
                        if (fs.Length == 0)
                        {
                            SaveHeader_CSV(sw);

                            sw.Write("Wavelength (nm),");
                            for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                                sw.Write(Scan.WaveLength[i] + ",");
                            sw.Write("\n");

                            sw.Write("Reference Signal (unitless),");
                            for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                                sw.Write(Scan.ReferenceIntensity[i] + ",");
                            sw.Write("\n");
                        }

                        sw.Write("Sample Signal (unitless),");
                        for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                            sw.Write(Scan.Intensity[i] + ",");
                        sw.Write("\n");
                    }
                }
            }
        }

        private void SaveToJCAMP(String FileName)
        {
            if (CheckBox_SaveIJDX.IsChecked == true)
            {
                String FileName_i = FileName.Insert(FileName.LastIndexOf("_", FileName.Length - 20), "_i");
                FileStream fs = new FileStream(FileName_i, FileMode.Create);
                SaveHeader(fs, out StreamWriter sw, true);

                sw.WriteLine("##XUNITS=Wavelength(nm)");
                sw.WriteLine("##YUNITS=Intensity");
                sw.WriteLine("##FIRSTX=" + Scan.WaveLength[0]);
                sw.WriteLine("##LASTX=" + Scan.WaveLength[Scan.ScanDataLen - 1]);
                sw.WriteLine("##PEAK TABLE=X+(Y..Y)");
                for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                {
                    sw.WriteLine(Scan.WaveLength[i] + "," + Scan.Intensity[i]);
                }
                sw.WriteLine("##END=");

                sw.Flush();  // Clear buffer
                sw.Close();  // Close file stream 
            }

            if (CheckBox_SaveAJDX.IsChecked == true)
            {
                String FileName_a = FileName.Insert(FileName.LastIndexOf("_", FileName.Length - 20), "_a");
                FileStream fs = new FileStream(FileName_a, FileMode.Create);
                SaveHeader(fs, out StreamWriter sw, true);

                sw.WriteLine("##XUNITS=Wavelength(nm)");
                sw.WriteLine("##YUNITS=Absorbance(AU)");
                sw.WriteLine("##FIRSTX=" + Scan.WaveLength[0]);
                sw.WriteLine("##LASTX=" + Scan.WaveLength[Scan.ScanDataLen - 1]);
                sw.WriteLine("##PEAK TABLE=X+(Y..Y)");
                for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                {
                    sw.WriteLine(Scan.WaveLength[i] + "," + Scan.Absorbance[i]);
                }
                sw.WriteLine("##END=");

                sw.Flush();  // Clear buffer
                sw.Close();  // Close file stream 
            }

            if (CheckBox_SaveRJDX.IsChecked == true)
            {
                String FileName_r = FileName.Insert(FileName.LastIndexOf("_", FileName.Length - 20), "_r");
                FileStream fs = new FileStream(FileName_r, FileMode.Create);
                SaveHeader(fs, out StreamWriter sw, true);

                sw.WriteLine("##XUNITS=Wavelength(nm)");
                sw.WriteLine("##YUNITS=Reflectance(AU)");
                sw.WriteLine("##FIRSTX=" + Scan.WaveLength[0]);
                sw.WriteLine("##LASTX=" + Scan.WaveLength[Scan.ScanDataLen - 1]);
                sw.WriteLine("##PEAK TABLE=X+(Y..Y)");
                for (Int32 i = 0; i < Scan.ScanDataLen; i++)
                {
                    sw.WriteLine(Scan.WaveLength[i] + "," + Scan.Reflectance[i]);
                }
                sw.WriteLine("##END=");

                sw.Flush();  // Clear buffer
                sw.Close();  // Close file stream 
            }
        }


        private void SaveToFiles()
        {
            String FileName = String.Empty;
            
            if (CheckBox_FileNamePrefix.IsChecked == true)
            {
                String Prefix = Helper.CheckRegex_Chinese(TextBox_FileNamePrefix.Text);
                if (Prefix.Length > 50)
                {
                    Prefix = Prefix.Substring(0, 50);
                    TextBox_FileNamePrefix.Text = Prefix;
                    MainWindow.ShowWarning("File name prefix is too long, only catch the first 50 characters.");
                }
                FileName = Path.Combine(Scan_Dir, Prefix + Scan.ScanConfigData.head.config_name + "_" + TimeScanStart.ToString("yyyyMMdd_HHmmss"));
            }
            else
            {
                FileName = Path.Combine(Scan_Dir, Scan.ScanConfigData.head.config_name + "_" + TimeScanStart.ToString("yyyyMMdd_HHmmss"));
            }

            SaveToCSV(FileName + ".csv");
            SaveToJCAMP(FileName + ".jdx");

            if (CheckBox_SaveDAT.IsChecked == true)
                Scan.SaveScanResultToBinFile(FileName + ".dat");  // For populating saved scan
            LoadSavedScanList();
        }

        #endregion

        #region Saved Scans

        List<Label> Label_SavedScanType     = new List<Label>();
        List<Label> Label_SavedRangeStart   = new List<Label>();
        List<Label> Label_SavedRangeEnd     = new List<Label>();
        List<Label> Label_SavedWidth        = new List<Label>();
        List<Label> Label_SavedDigRes       = new List<Label>();
        List<Label> Label_SavedExposure     = new List<Label>();

        private void ClearSavedScanCfgItems()
        {
            for (Int32 i = 0; i < MAX_CFG_SECTION; i++)
            {
                Label_SavedScanType[i].Content      = String.Empty;
                Label_SavedRangeStart[i].Content    = String.Empty;
                Label_SavedRangeEnd[i].Content      = String.Empty;
                Label_SavedWidth[i].Content         = String.Empty;
                Label_SavedDigRes[i].Content        = String.Empty;
                Label_SavedExposure[i].Content      = String.Empty;
            }
            Label_SavedAvg.Content                  = String.Empty;
        }

        private void InitSavedScanCfgItems()
        {
            Label_SavedScanType.Clear();
            Label_SavedScanType.Add(Label_SavedScanType1);
            Label_SavedScanType.Add(Label_SavedScanType2);
            Label_SavedScanType.Add(Label_SavedScanType3);
            Label_SavedScanType.Add(Label_SavedScanType4);
            Label_SavedScanType.Add(Label_SavedScanType5);
            Label_SavedRangeStart.Clear();
            Label_SavedRangeStart.Add(Label_SavedRangeStart1);
            Label_SavedRangeStart.Add(Label_SavedRangeStart2);
            Label_SavedRangeStart.Add(Label_SavedRangeStart3);
            Label_SavedRangeStart.Add(Label_SavedRangeStart4);
            Label_SavedRangeStart.Add(Label_SavedRangeStart5);
            Label_SavedRangeEnd.Clear();
            Label_SavedRangeEnd.Add(Label_SavedRangeEnd1);
            Label_SavedRangeEnd.Add(Label_SavedRangeEnd2);
            Label_SavedRangeEnd.Add(Label_SavedRangeEnd3);
            Label_SavedRangeEnd.Add(Label_SavedRangeEnd4);
            Label_SavedRangeEnd.Add(Label_SavedRangeEnd5);
            Label_SavedWidth.Clear();
            Label_SavedWidth.Add(Label_SavedWidth1);
            Label_SavedWidth.Add(Label_SavedWidth2);
            Label_SavedWidth.Add(Label_SavedWidth3);
            Label_SavedWidth.Add(Label_SavedWidth4);
            Label_SavedWidth.Add(Label_SavedWidth5);
            Label_SavedDigRes.Clear();
            Label_SavedDigRes.Add(Label_SavedDigRes1);
            Label_SavedDigRes.Add(Label_SavedDigRes2);
            Label_SavedDigRes.Add(Label_SavedDigRes3);
            Label_SavedDigRes.Add(Label_SavedDigRes4);
            Label_SavedDigRes.Add(Label_SavedDigRes5);
            Label_SavedExposure.Clear();
            Label_SavedExposure.Add(Label_SavedExposure1);
            Label_SavedExposure.Add(Label_SavedExposure2);
            Label_SavedExposure.Add(Label_SavedExposure3);
            Label_SavedExposure.Add(Label_SavedExposure4);
            Label_SavedExposure.Add(Label_SavedExposure5);

            ClearSavedScanCfgItems();
        }

        private List<String> EnumerateFiles(String SearchPattern)
        {
            List<String> ListFiles = new List<String>();

            try
            {
                String DirPath = TextBox_DisplayDirPath.Text;

                foreach (String Files in Directory.EnumerateFiles(DirPath, SearchPattern))
                {
                    String FileName = Files.Substring(Files.LastIndexOf("\\") + 1);
                    ListFiles.Add(FileName);
                }
            }
            catch (UnauthorizedAccessException UAEx) { DBG.WriteLine(UAEx.Message); }
            catch (PathTooLongException PathEx) { DBG.WriteLine(PathEx.Message); }

            return ListFiles;
        }

        private void LoadSavedScanList()
        {
            List<String> ListFiles = EnumerateFiles("*.dat");
            ListView_SavedData.Items.Clear();
            foreach (String FileName in ListFiles)
            {
                ListView_SavedData.Items.Add(FileName);
            }
        }

        private void TextBox_DisplayDirPath_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return)  // Enter = Return
            {
                LoadSavedScanList();
                ClearSavedScanCfgItems();
            }
        }

        private void Button_DisplayDirChange_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();

            dlg.SelectedPath = TextBox_DisplayDirPath.Text;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Display_Dir = dlg.SelectedPath;
                TextBox_DisplayDirPath.Text = dlg.SelectedPath;

                LoadSavedScanList();
                ClearSavedScanCfgItems();
            }
        }

        private void ListView_SavedData_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView_SavedData.SelectedIndex < 0)
                return;

            String item = ListView_SavedData.SelectedItem.ToString();
            String FileName = Path.Combine(TextBox_DisplayDirPath.Text, item);

            // Read scan result and populate to the buffer
            if (Scan.ReadScanResultFromBinFile(FileName) == SDK.FAIL)
            {
                DBG.WriteLine("Read file failed!");
                MainWindow.ShowError("Read file failed!\nThis file may not match the format!");
                return;
            }

            Scan.GetScanResult();

            // Draw the scan result
            SpectrumPlot();

            // Populate config data
            ClearSavedScanCfgItems();

            for (Int32 i = 0; i < Scan.ScanConfigData.head.num_sections; i++)
            {
                Label_SavedScanType[i].Content      = Helper.ScanTypeIndexToMode(Scan.ScanConfigData.section[i].section_scan_type).Substring(0, 3);
                Label_SavedRangeStart[i].Content    = Scan.ScanConfigData.section[i].wavelength_start_nm;
                Label_SavedRangeEnd[i].Content      = Scan.ScanConfigData.section[i].wavelength_end_nm;
                Label_SavedWidth[i].Content         = Math.Round(Helper.CfgWidthPixelToNM(Scan.ScanConfigData.section[i].width_px), 2);
                Label_SavedDigRes[i].Content        = Scan.ScanConfigData.section[i].num_patterns;
                Label_SavedExposure[i].Content      = Helper.CfgExpIndexToTime(Scan.ScanConfigData.section[i].exposure_time);
            }
            Label_SavedAvg.Content                  = Scan.ScanConfigData.head.num_repeats;
        }

        #endregion
    }
}
