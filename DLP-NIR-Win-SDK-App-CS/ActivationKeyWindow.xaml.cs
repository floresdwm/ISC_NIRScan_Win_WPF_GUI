/***************************************************************************/
/*                  Copyright (c) 2018 Inno Spectra Corp.                  */
/*                           ALL RIGHTS RESERVED                           */
/***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;
using DLP_NIR_Win_SDK_CS;

namespace DLP_NIR_Win_SDK_App_CS
{
    public class ListViewData
    {
        public ListViewData()
        {
            // default constructor
        }

        public ListViewData(String sernum, String key)
        {
            SerNum = sernum;
            Key = key;
        }

        public String SerNum { get; set; }
        public String Key { get; set; }
    }

    public partial class ActivationKeyWindow : Window
    {
        private Boolean KeyBack = false;

        public ActivationKeyWindow()
        {
            InitializeComponent();
            Loaded += new RoutedEventHandler(ActivationKeyWindow_Loaded);
            this.Title = "Activation Key Management";

            // Setup the window position to application center
            Application curApp = Application.Current;
            Window mainWindow = curApp.MainWindow;
            this.Left = mainWindow.Left + (mainWindow.Width - this.Width) / 2;
            this.Top = mainWindow.Top + (mainWindow.Height - this.Height) / 2;
        }

        private void ActivationKeyWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ListView_Data.Items.Clear();
            
            foreach (ListViewData row in ReadFromFile())
                ListView_Data.Items.Add(row);  // Add data to list view

            TextBox_SN.Focus();
        }

        private void TextBox_Key_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Back)
                KeyBack = true;
            else
                KeyBack = false;
        }
        //用來比較textkey是否有處理
        String tmpkey = "";
        private void TextBox_Key_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(tmpkey != TextBox_Key.Text)
            {
                Regex rgx = new Regex("[^a-fA-F0-9]");
                String key = TextBox_Key.Text;
                key = rgx.Replace(key, "");
                //暫存處理key
                String buf = "";
                for (int i = 0; i < key.Length; i++)
                {
                    buf += key.Substring(i, 1);
                    if (i % 2 == 1 && i!=key.Length-1)
                    {
                        buf += " ";
                    }
                }
                tmpkey = buf;
                TextBox_Key.Text = buf;
                //將游標移到最後
                TextBox_Key.Select(TextBox_Key.Text.Length, 0);
            }
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            String[] key = TextBox_Key.Text.Split(new char[] { ' ', ':', ';', '-', '_' });
            List<ListViewData> ItemsList = new List<ListViewData>();
            Boolean isNewData = true;

            if (TextBox_Key.Text.Length != 35 || key.Length != 12)
            {
                MainWindow.ShowError("The key format is incorrect, please check if the string outside the space is 2 characters 1 group, and total 12 groups.");
                return;
            }

            // Read data from list view
            foreach (ListViewData data in ListView_Data.Items)
                ItemsList.Add(data);

            for (int index = 0; index < ItemsList.Count; index++)
            {
                if (ItemsList[index].SerNum == TextBox_SN.Text)
                {
                    String text = "Do you want to replace " + TextBox_SN.Text + "'s key?";
                    MessageBoxResult result = MainWindow.ShowQuestion(text, MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK)
                    {
                        ListView_Data.Items.RemoveAt(index);
                        ListView_Data.Items.Insert(index, new ListViewData(TextBox_SN.Text, TextBox_Key.Text));
                        ListView_Data.SelectedIndex = index;
                        isNewData = false;
                        break;
                    }
                    else
                        return;
                }
            }

            if (isNewData)
            {
                ListView_Data.Items.Add(new ListViewData(TextBox_SN.Text, TextBox_Key.Text));
                ListView_Data.SelectedIndex = ListView_Data.Items.Count - 1;
            }

            ItemsList.Clear();
            foreach (ListViewData data in ListView_Data.Items)
                ItemsList.Add(data);
            SaveToFile(ItemsList);

            TextBox_SN.Text = "";
            TextBox_Key.Text = "";
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            int index = ListView_Data.SelectedIndex;
            if (index < 0)
            {
                MainWindow.ShowError("No item can be deleted.");
                return;
            }

            ListView_Data.Items.RemoveAt(index);

            if (index <= ListView_Data.Items.Count - 1)
                ListView_Data.SelectedIndex = index;  // Select next row
            else
                ListView_Data.SelectedIndex = ListView_Data.Items.Count - 1;  // Select last row

            List<ListViewData> ItemsList = new List<ListViewData>();
            foreach (ListViewData data in ListView_Data.Items)
                ItemsList.Add(data);
            SaveToFile(ItemsList);
        }

        private void Button_Apply_Click(object sender, RoutedEventArgs e)
        {
            if (ListView_Data.SelectedIndex < 0)
            {
                MainWindow.ShowError("No item can apply.");
                return;
            }

            ListViewData lvc = (ListViewData)ListView_Data.SelectedItem;
            String[] StrKey = lvc.Key.Split(new char[] { ' ', ':', ';', '-', '_' });
            String status = String.Empty;
            Byte[] ByteKey = new Byte[12];

            for (int i = 0; i < StrKey.Length; i++)
            {
                try { ByteKey[i] = Convert.ToByte(StrKey[i], 16); }
                catch { ByteKey[i] = 0; }
            }

            Device.SetActivationKey(ByteKey);
            status = IsActivated ? "PASS!" : "FAILED!";
            StatusBarItem_KeyStatus.Content = "Device (" + lvc.SerNum + ") key applies " + status;
            if (!IsActivated)
                MainWindow.ShowError("The key applies FAILED!\n\n" +
                                     "Please check relevant information.");
        }

        private void Button_GetSerNum_Click(object sender, RoutedEventArgs e)
        {
            TextBox_SN.Text = Device.DevInfo.SerialNumber;
            TextBox_Key.Focus();
        }

        private void SaveToFile(IEnumerable<object> rows)
        {
            String FileName = Path.Combine(MainWindow.ConfigDir, "ActictionKey.xml");
            DBG.WriteLine("Save key pairs to {0}", FileName);

            // Save data to file
            XmlSerializer xml = new XmlSerializer(typeof(List<ListViewData>));
            TextWriter writer = new StreamWriter(FileName);
            xml.Serialize(writer, rows);
            writer.Close();
        }

        private IEnumerable<object> ReadFromFile()
        {
            String FileName = Path.Combine(MainWindow.ConfigDir, "ActictionKey.xml");
            DBG.WriteLine("Read key pairs from {0}", FileName);
            List<ListViewData> rows = new List<ListViewData>();

            if (File.Exists(FileName))
            {
                XmlSerializer xml = new XmlSerializer(typeof(List<ListViewData>));
                TextReader reader = new StreamReader(FileName);
                rows = (List<ListViewData>)xml.Deserialize(reader);
                reader.Close();
            }

            return rows;
        }

        public void ChangeSerialNumber(String OldNum, String NewNum)
        {
            List<ListViewData> ItemsList = new List<ListViewData>();
            ItemsList = (List<ListViewData>)ReadFromFile();

            for (int index = 0; index < ItemsList.Count; index++)
            {
                if (ItemsList[index].SerNum == OldNum)
                {
                    DBG.WriteLine("Change <{0}> to <{1}> successfully!", ItemsList[index].SerNum, NewNum);
                    ItemsList[index].SerNum = NewNum;
                    break;
                }
            }
            SaveToFile(ItemsList);
        }

        public bool CheckActivationKey()
        {
            List<ListViewData> ItemsList = new List<ListViewData>();
            ItemsList = (List<ListViewData>)ReadFromFile();
            bool result = false;

            for (int index = 0; index < ItemsList.Count; index++)
            {
                if (ItemsList[index].SerNum == Device.DevInfo.SerialNumber)
                {
                    String[] StrKey = ItemsList[index].Key.Split(new char[] { ' ', ':', ';', '-', '_' });
                    Byte[] ByteKey = new Byte[12];

                    for (int i = 0; i < StrKey.Length; i++)
                    {
                        try { ByteKey[i] = Convert.ToByte(StrKey[i], 16); }
                        catch { ByteKey[i] = 0; }
                    }

                    DBG.WriteLine("Check <{0}> activation key...", ItemsList[index].SerNum);
                    Device.SetActivationKey(ByteKey);
                    result = true;
                    break;
                }
            }

            if (!result)
            {
                DBG.WriteLine("No activation key inside the key pairs.");
                return false;
            }
            else
                return IsActivated;
        }

        public bool IsActivated { get { if (Device.GetActivationResult() == 1) return true; else return false; } }
    }
}
