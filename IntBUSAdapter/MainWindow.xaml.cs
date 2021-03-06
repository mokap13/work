﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.IO.Ports;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

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
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = false;
            //this.intbusConfigurator.Close();
            this.rootIntbusDevice = null;
            SerialPortModbus.DataReceived -= new SerialDataReceivedEventHandler(SerialDataModbusReceived);
            this.intbusConfigurator.Close(e);
            base.OnClosing(e);
        }
        private int expectByteCount;

        public SerialPort SerialPortModbus { get; set; }
        public SerialPort SerialPortIntBUS { get; set; }
        public ObservableCollection<string> Parities { get; set; }
        public ObservableCollection<string> StopBites { get; set; }
        public ObservableCollection<int> Bits { get; set; }
        public ObservableCollection<int> BaudRates { get; set; }
        public ObservableCollection<string> SerialPortNames { get; set; }
        public List<byte> Preambula { get; set; }
        public List<byte> IntBusAddedData { get; set; }
        private IntbusDevice rootIntbusDevice;
        IntbusConfigurator intbusConfigurator;

        private int responseTimeOut = 100; 

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            intbusConfigurator = new IntbusConfigurator();
            rootIntbusDevice = intbusConfigurator.IntbusDevice;

            SerialPortModbus = new SerialPort();
            SerialPortModbus.DataReceived += new SerialDataReceivedEventHandler(SerialDataModbusReceived);
            SerialPortIntBUS = new SerialPort();

            IntBusAddedData = new List<byte>();
            Preambula = new List<byte>();
            SerialPortNames = new ObservableCollection<string>();
            Parities = new ObservableCollection<string>();
            StopBites = new ObservableCollection<string>();
            Bits = new ObservableCollection<int>() { 5, 6, 7, 8 };
            BaudRates = new ObservableCollection<int>()
            {
                50, 1200, 2400, 4800, 9600, 19200, 38400,
                56000, 57600, 76800, 115200, 230400, 460800
            };
            expectByteCount = 1;
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
            byte[] receivedBytes = new byte[byteRecieved];
            (sender as SerialPort).Read(receivedBytes, 0, byteRecieved);

            int mbAddress = receivedBytes.First();
            IntbusDevice device;
            if (IntbusDevice.ModbusDeviceAddresses.ContainsKey(mbAddress) == true)
                device = IntbusDevice.ModbusDeviceAddresses[mbAddress];
            else
                return;
            string message = null;


            List<byte> listBytes = new List<byte>();
            listBytes.AddRange(receivedBytes);

            listBytes = device.ConvertToIntbusFrame(listBytes);

            if (SerialPortIntBUS != null)
                if (SerialPortIntBUS.IsOpen)
                {
                    message = null;
                    for (int i = 0; i < listBytes.Count; i++)
                        message += String.Format("{0:X2} ", listBytes[i]);
                    /////////////////////////////////
                    receivedBytes = listBytes.ToArray();
                    SerialPortIntBUS.DiscardOutBuffer();
                    SerialPortIntBUS.DiscardInBuffer();
                    SerialPortIntBUS.Write(receivedBytes, 0, receivedBytes.Length);
                    
                    Task.Factory.StartNew(() =>
                    {
                        Thread.Sleep(responseTimeOut);
                        SerialDataIntBUSResponse();
                    });
                    message = AddTime(message);
                    AddConsoleText(message + "\r", textBox_ConsoleIntBUS, Brushes.NavajoWhite);
                }
        }

        private string AddTime(string str)
        {
            return str.Insert(0, DateTime.Now.ToString("HH:mm:ss.fff") + "    ");
        }

        private void SerialDataIntBUSResponse()
        {
            int byteRecieved = SerialPortIntBUS.BytesToRead;
            byte[] responseBytes = new byte[byteRecieved];
            if (responseBytes.Length == 0)
                return;
            SerialPortIntBUS.Read(responseBytes, 0, byteRecieved);
            SerialPortIntBUS.DiscardInBuffer();

            string message = BitConverter.ToString(responseBytes).Replace('-', ' ');
            message = AddTime(message);
            AddConsoleText(message + "\r", textBox_ConsoleIntBUS, Brushes.LimeGreen);

            List<byte> listBytes = new List<byte>();
            listBytes.AddRange(responseBytes);
            listBytes = listBytes.Skip(2).ToList();//TODO переделать
            listBytes = listBytes.Take(listBytes.Count() - 2).ToList();
            listBytes.AddRange(ModbusUtility.CalculateCrc(listBytes.ToArray()));
            responseBytes = listBytes.ToArray();

            message = BitConverter.ToString(responseBytes).Replace('-', ' ');
            message = AddTime(message);
            if (SerialPortModbus != null)
                if (SerialPortModbus.IsOpen)
                {
                    SerialPortModbus.Write(responseBytes, 0, responseBytes.Length);
                }
            
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
        private void Preambula_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string[] data = (sender as TextBox).Text.Split(' ');
                Preambula.Clear();
                foreach (string strByte in data)
                {
                    Preambula.Add(byte.Parse(strByte, NumberStyles.HexNumber));
                }
                Preambula.Reverse();
            }
            catch (Exception)
            {


            }
        }
        #endregion


        private void SerialTimeouIntBUS_TextChanged(object sender, TextChangedEventArgs e)
        {
            string value = (sender as TextBox).Text;
            int.TryParse(value, out int ivalue);
            responseTimeOut = ivalue;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            intbusConfigurator.Show();
        }
    }
}
