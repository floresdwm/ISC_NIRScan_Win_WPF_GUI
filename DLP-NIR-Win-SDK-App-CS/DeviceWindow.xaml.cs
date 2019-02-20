/***************************************************************************/
/*                  Copyright (c) 2018 Inno Spectra Corp.                  */
/*                           ALL RIGHTS RESERVED                           */
/***************************************************************************/

using System;
using System.Windows;
using DLP_NIR_Win_SDK_CS;

namespace DLP_NIR_Win_SDK_App_CS
{
    public partial class DeviceWindow : Window
    {
        /**
         * This action sends an index to connect the selected device.
         * The receiver is in MainWindow.
         */
        public event Action<int> UserSelection = null;

        /**
         * This function is the main fuinction of Device Selection Window.
         * The window displays all available devices.
         */
        public DeviceWindow()
        {
            InitializeComponent();
            Loaded += DeviceWindow_Loaded;
            for (int i = 0; i < Device.DeviceCounts; i++)
            {
                String deviceName = Device.DeviceFound[i].ProductString + " (" + Device.DeviceFound[i].SerialNumber + ")";
                ListBox_Devices.Items.Add(deviceName);
            }
            ListBox_Devices.SelectedIndex = 0;
        }

        /**
         * This function loads the window position when window is opened.
         * 
         * @param sender    -I- none.
         * @param e         -I- none.
         */
        private void DeviceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Application curApp = Application.Current;
            Window mainWindow = curApp.MainWindow;
            this.Left = mainWindow.Left + (mainWindow.Width - this.ActualWidth) / 2;
            this.Top = mainWindow.Top + (mainWindow.Height - this.ActualHeight) / 2;
        }

        /**
         * This function sends the selected device index, and closing the device selection window.
         * 
         * @param sender    -I- none.
         * @param e         -I- none.
         */
        private void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            UserSelection(ListBox_Devices.SelectedIndex);
            this.Close();
        }
    }
}
