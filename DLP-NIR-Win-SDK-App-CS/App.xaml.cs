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
            if (Device.IsConnected() && Device.ChkBleExist() == 1)
                Device.SetBluetooth(true);

            Device.Close();
            Device.Exit();
        }
    }
}
