using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Modbus.IO;
using Modbus;
using System.IO.Ports;
using Modbus.Device;
using Modbus.Utility;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Modbus.Data;
using System.Globalization;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;

namespace IntBUSAdapter
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBooleanConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public SerialPort SerialPortModbus { get; set; }
        public SerialPort SerialPortIntBUS { get; set; }
        public ObservableCollection<string> Parities { get; set; }
        public ObservableCollection<string> StopBites { get; set; }
        public ObservableCollection<int> Bits { get; set; }
        public ObservableCollection<int> BaudRates { get; set; }
        public ObservableCollection<string> SerialPortNames { get; set; }
        public List<byte> IntBusAddedData { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            SerialPortModbus = new SerialPort();
            SerialPortModbus.DataReceived += new SerialDataReceivedEventHandler(SerialDataModbusReceived);
            SerialPortIntBUS = new SerialPort();
            SerialPortIntBUS.DataReceived += new SerialDataReceivedEventHandler(SerialDataIntBUSResponse);
            IntBusAddedData = new List<byte>();
            SerialPortNames = new ObservableCollection<string>();
            Parities = new ObservableCollection<string>();
            StopBites = new ObservableCollection<string>();
            Bits = new ObservableCollection<int>() { 5, 6, 7, 8 };
            BaudRates = new ObservableCollection<int>()
            {
                50, 1200, 2400, 4800, 9600, 19200, 38400,
                56000, 57600, 76800, 115200, 230400, 460800
            };

            foreach (string name in SerialPort.GetPortNames())
                SerialPortNames.Add(name);

            foreach (string parityName in Enum.GetNames(typeof(Parity)))
                Parities.Add(parityName);

            foreach (string stopBitsName in Enum.GetNames(typeof(StopBits)))
                StopBites.Add(stopBitsName);
        }

        private void SerialDataModbusReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int byteRecieved = (sender as SerialPort).BytesToRead;
            byte[] messByte = new byte[byteRecieved];
            (sender as SerialPort).Read(messByte, 0, byteRecieved);

            string message = null;
            for (int i = 0; i < messByte.Length; i++)
                message += String.Format("{0:X2} ", messByte[i]);
            message = AddTime(message);
            AddConsoleText(message + "\r", textBox_ConsoleModbus, Brushes.NavajoWhite);

            List<byte> listBytes = new List<byte>();
            listBytes.AddRange(messByte);
            /////*******************//////////*******************/////
            for (int i = 0; i < IntBusAddedData.Count; i++)
            {
                listBytes.Insert(0, IntBusAddedData[i]);
                listBytes.AddRange(ModbusUtility.CalculateCrc(listBytes.ToArray()));
            }

           
            if (SerialPortIntBUS != null)
                if (SerialPortIntBUS.IsOpen)
                {
                    message = null;
                    for (int i = 0; i < listBytes.Count; i++)
                        message += String.Format("{0:X2} ", listBytes[i]);
                    /////////////////////////////////
                    messByte = listBytes.ToArray();
                    SerialPortIntBUS.Write(messByte, 0, messByte.Length);
                    message = AddTime(message);
                    AddConsoleText(message + "\r", textBox_ConsoleIntBUS, Brushes.NavajoWhite);
                }
        }
   
        private string AddTime(string str)
        {
            return str.Insert(0, DateTime.Now.ToString("HH:mm:ss.fff") + "    ");
        }
        private void SerialDataIntBUSResponse(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort serialPort = (SerialPort)sender;
            int byteRecieved = serialPort.BytesToRead;
            byte[] messByte = new byte[byteRecieved];
            serialPort.Read(messByte, 0, byteRecieved);

            string message = BitConverter.ToString(messByte).Replace('-', ' ');
            message = AddTime(message);

            if (SerialPortModbus != null)
                if(SerialPortModbus.IsOpen)
                {
                    SerialPortModbus.Write(messByte, 0, messByte.Length);
                    AddConsoleText(message + "\r", textBox_ConsoleModbus, Brushes.LimeGreen);
                }
            AddConsoleText(message + "\r", textBox_ConsoleIntBUS, Brushes.LimeGreen);
        }
        void AddConsoleText(string message, RichTextBox textBox, object color)
        {
            textBox.Dispatcher.Invoke(new Action(() =>
            {
                TextRange range = new TextRange(textBox.Document.ContentEnd, textBox.Document.ContentEnd)
                {
                    Text = message
                };
                range.ApplyPropertyValue(TextElement.ForegroundProperty, color);
                textBox.ScrollToEnd();
            }));
        }

        #region IntBus

        private void ComboBox_PortName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerialPortIntBUS.PortName = (sender as ComboBox).SelectedValue.ToString();
        }

        private void ComboBox_Parities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerialPortIntBUS.Parity =
                (Parity)Enum.Parse(typeof(Parity),
                (sender as ComboBox).SelectedValue.ToString());
        }

        private void ComboBox_StopBites_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerialPortIntBUS.StopBits =
                (StopBits)Enum.Parse(typeof(StopBits),
                (sender as ComboBox).SelectedValue.ToString());
        }

        private void ComboBox_Bits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerialPortIntBUS.DataBits =
                int.Parse((sender as ComboBox).SelectedValue.ToString());
        }

        private void ComboBox_BaudRates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerialPortIntBUS.BaudRate =
                int.Parse((sender as ComboBox).SelectedValue.ToString());
        }

        private void ToggleButton_OpenOutPort_Checked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (sender as ToggleButton);
            
            try
            {
                if (!SerialPortIntBUS.IsOpen)
                {
                    SerialPortIntBUS.Open();
                    if (SerialPortIntBUS.IsOpen)
                    {
                        label_PortState_IntBUS.Content = "Порт открыт";
                        button.Content = "Закрыть";
                    }
                }
                else
                {
                    button.IsChecked = true;
                    throw new Exception($"Доступ к порту '{SerialPortIntBUS.PortName.ToString()}' закрыт");
                }
            }
            catch (Exception exception)
            {
                button.IsChecked = true;
                label_PortState_IntBUS.Content = exception.Message;
            }
        }
        private void ToggleButton_OpenOutPort_Unchecked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (sender as ToggleButton);
            try
            {
                if (SerialPortIntBUS.IsOpen)
                {
                    SerialPortIntBUS.Close();
                    if (!SerialPortIntBUS.IsOpen)
                    {
                        label_PortState_IntBUS.Content = "Порт закрыт";
                        button.Content = "Открыть";
                    }
                }
                else
                {
                    button.IsChecked = false;
                    throw new Exception($"Доступ к порту '{SerialPortIntBUS.PortName.ToString()}' закрыт");
                }
            }
            catch (Exception exception)
            {
                label_PortState_IntBUS.Content = exception.Message;
                button.IsChecked = false;
            }
        }
        #endregion

        #region Modbus
        private void ComboBox_PortName_SelectionChanged2(object sender, SelectionChangedEventArgs e)
        {
            SerialPortModbus.PortName = (sender as ComboBox).SelectedValue.ToString();
        }

        private void ComboBox_Parities_SelectionChanged2(object sender, SelectionChangedEventArgs e)
        {
            SerialPortModbus.Parity =
                (Parity)Enum.Parse(typeof(Parity),
                (sender as ComboBox).SelectedValue.ToString());
        }

        private void ComboBox_StopBites_SelectionChanged2(object sender, SelectionChangedEventArgs e)
        {
            SerialPortModbus.StopBits =
                (StopBits)Enum.Parse(typeof(StopBits),
                (sender as ComboBox).SelectedValue.ToString());
        }

        private void ComboBox_Bits_SelectionChanged2(object sender, SelectionChangedEventArgs e)
        {
            SerialPortModbus.DataBits =
                int.Parse((sender as ComboBox).SelectedValue.ToString());
        }

        private void ComboBox_BaudRates_SelectionChanged2(object sender, SelectionChangedEventArgs e)
        {
            SerialPortModbus.BaudRate =
                int.Parse((sender as ComboBox).SelectedValue.ToString());
        }

        private void ToggleButton_SerialPortIn_Unchecked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (sender as ToggleButton);
            try
            {
                if (SerialPortModbus.IsOpen)
                {
                    SerialPortModbus.Close();
                    if (!SerialPortModbus.IsOpen)
                    {
                        label_PortState_Modbus.Content = "Порт закрыт";
                        button.Content = "Открыть";
                    }
                }
                else
                {
                    button.IsChecked = false;
                    throw new Exception($"Доступ к порту '{SerialPortModbus.PortName.ToString()}' закрыт");
                }
            }
            catch (Exception exception)
            {
                label_PortState_Modbus.Content = exception.Message;
                button.IsChecked = false;
            }
            //ToggleButton button = (sender as ToggleButton);

            //try
            //{
            //    if (!SerialPortIn.IsOpen)
            //    {
            //        SerialPortIn.Open();
            //        if (SerialPortIn.IsOpen)
            //        {
            //            label_PortState_In.Content = "Порт открыт";
            //            button.Content = "Закрыть";
            //        }
            //    }
            //    else
            //    {
            //        button.IsChecked = false;
            //        throw new Exception($"Доступ к порту '{SerialPortIn.PortName.ToString()}' закрыт");
            //    }
            //}
            //catch (Exception exception)
            //{
            //    label_PortState_In.Content = exception.Message;
            //    button.IsChecked = false;
            //}
        }

        private void ToggleButton_SerialPortIn_Checked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (sender as ToggleButton);

            try
            {
                if (!SerialPortModbus.IsOpen)
                {
                    SerialPortModbus.Open();
                    if (SerialPortModbus.IsOpen)
                    {
                        label_PortState_Modbus.Content = "Порт открыт";
                        button.Content = "Закрыть";
                    }
                }
                else
                {
                    button.IsChecked = true;
                    throw new Exception($"Доступ к порту '{SerialPortModbus.PortName.ToString()}' закрыт");
                }
            }
            catch (Exception exception)
            {
                button.IsChecked = true;
                label_PortState_Modbus.Content = exception.Message;
            }


            //ToggleButton button = (sender as ToggleButton);
            //try
            //{
            //    if (SerialPortIn.IsOpen)
            //    {
            //        SerialPortIn.Close();
            //        if (!SerialPortIn.IsOpen)
            //        {
            //            label_PortState_In.Content = "Порт закрыт";
            //            button.Content = "Открыть";
            //        }
            //    }
            //    else
            //    {
            //        button.IsChecked = true;
            //        throw new Exception($"Доступ к порту '{SerialPortIn.PortName.ToString()}' закрыт");
            //    }
            //}
            //catch (Exception exception)
            //{
            //    label_PortState_In.Content = exception.Message;
            //    button.IsChecked = true;
            //}
        }
        private void TextBox_IntBusData_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string[] data = (sender as TextBox).Text.Split(' ');
                IntBusAddedData.Clear();
                foreach (string strByte in data)
                {
                    IntBusAddedData.Add(byte.Parse(strByte, NumberStyles.HexNumber));
                }
                IntBusAddedData.Reverse();
            }
            catch (Exception)
            {


            }

        }

        #endregion

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
    }
}
