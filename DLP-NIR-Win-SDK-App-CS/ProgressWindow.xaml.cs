/***************************************************************************/
/*                  Copyright (c) 2018 Inno Spectra Corp.                  */
/*                           ALL RIGHTS RESERVED                           */
/***************************************************************************/

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace DLP_NIR_Win_SDK_App_CS
{
    public class MyModel : INotifyPropertyChanged
    {
        private bool _Btn_Hidden;
        public bool Btn_Hidden
        {
            get { return _Btn_Hidden; }
            set
            {
                _Btn_Hidden = value;
                OnPropertyChanged("Btn_Hidden");
            }
        }

        private bool _Height_Extended;
        public bool Height_Extended
        {
            get { return _Height_Extended; }
            set
            {
                _Height_Extended = value;
                OnPropertyChanged("Height_Extended");
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
    public partial class ProgressWindow : Window
    {
        private MyModel MyParams;

        public String info
        {
            get {
                return (String)Label_Progress_Info.Content;
            }
            set {
                Label_Progress_Info.Content = value;
            }
        }
        public ProgressWindow()
        {
            InitializeComponent();
            MyParams = new MyModel();
            this.DataContext = MyParams;
        
            // Setup the MainWindow Position 
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

            if (MainWindow.ProgressWindow_left == 0 && MainWindow.ProgressWindow_top == 0)// Setup the MainWindow Position to center desktop screen
            {
                this.Left = thisLeft;
                this.Top = thisTop;
            }
            else
            {
                this.Left = MainWindow.ProgressWindow_left;
                this.Top = MainWindow.ProgressWindow_top;
            }
        }

        private bool _cancel = false;
        public bool IsCancelled { get { return _cancel; } }

        public bool ButtonEnabled
        {
            set
            {
                MyParams.Btn_Hidden = !value;
                MyParams.Height_Extended = value;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _cancel = true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {           
            this.DragMove(); // open can move window
        }
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {

            MainWindow.ProgressWindow_top = this.Top;
            MainWindow.ProgressWindow_left = this.Left;
        }

    }
}
