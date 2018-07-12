/***************************************************************************/
/*                  Copyright (c) 2018 Inno Spectra Corp.                  */
/*                           ALL RIGHTS RESERVED                           */
/***************************************************************************/

using System;
using System.Windows;
using DLP_NIR_Win_SDK_CS;

namespace DLP_NIR_Win_SDK_App_CS
{
    public partial class App : Application
    {
        /**
         * This function is to close application.
         * 
         * @param sender    -I- none.
         * @param e         -I- none.
         */
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (Device.IsConnected())
            {
                String HWRev = (!String.IsNullOrEmpty(Device.DevInfo.HardwareRev)) ? Device.DevInfo.HardwareRev.Substring(0, 1) : String.Empty;
                if ((HWRev != "B" && HWRev != String.Empty && Device.ChkBleExist() == 1) || HWRev == "B" || HWRev == String.Empty)
                    Device.SetBluetooth(true);
            }

            Device.Close();
            Device.Exit();
        }
    }
}
