using System;
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
        private int expectByteCount;

        public List<byte> Preambula { get; set; }
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
            //SerialPortIntBUS.ReadTimeout = 5;
            SerialPortIntBUS.DataReceived += new SerialDataReceivedEventHandler(SerialDataIntBUSResponse);
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
                listBytes.RemoveRange(listBytes.Count - 2, 2);
                listBytes.AddRange(ModbusUtility.CalculateCrc(listBytes.ToArray()));
            }

            listBytes.InsertRange(0, Preambula);

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
            byte[] messByte = new byte[byteRecieved]; ;
            if (messByte.Length < expectByteCount)
                return;
            serialPort.Read(messByte, 0, byteRecieved);
            serialPort.DiscardInBuffer();
            string message = BitConverter.ToString(messByte).Replace('-', ' ');
            message = AddTime(message);

            if (SerialPortModbus != null)
                if (SerialPortModbus.IsOpen)
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

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        private void SerialTimeouIntBUS_TextChanged(object sender, TextChangedEventArgs e)
        {
            string value = (sender as TextBox).Text;
            int.TryParse(value, out int ivalue);
            expectByteCount = ivalue;
        }
    }


    /// <summary>
	/// Modbus utility methods.
	/// </summary>
	public static class ModbusUtility
    {
        private static readonly ushort[] crcTable = {
            0X0000, 0XC0C1, 0XC181, 0X0140, 0XC301, 0X03C0, 0X0280, 0XC241,
            0XC601, 0X06C0, 0X0780, 0XC741, 0X0500, 0XC5C1, 0XC481, 0X0440,
            0XCC01, 0X0CC0, 0X0D80, 0XCD41, 0X0F00, 0XCFC1, 0XCE81, 0X0E40,
            0X0A00, 0XCAC1, 0XCB81, 0X0B40, 0XC901, 0X09C0, 0X0880, 0XC841,
            0XD801, 0X18C0, 0X1980, 0XD941, 0X1B00, 0XDBC1, 0XDA81, 0X1A40,
            0X1E00, 0XDEC1, 0XDF81, 0X1F40, 0XDD01, 0X1DC0, 0X1C80, 0XDC41,
            0X1400, 0XD4C1, 0XD581, 0X1540, 0XD701, 0X17C0, 0X1680, 0XD641,
            0XD201, 0X12C0, 0X1380, 0XD341, 0X1100, 0XD1C1, 0XD081, 0X1040,
            0XF001, 0X30C0, 0X3180, 0XF141, 0X3300, 0XF3C1, 0XF281, 0X3240,
            0X3600, 0XF6C1, 0XF781, 0X3740, 0XF501, 0X35C0, 0X3480, 0XF441,
            0X3C00, 0XFCC1, 0XFD81, 0X3D40, 0XFF01, 0X3FC0, 0X3E80, 0XFE41,
            0XFA01, 0X3AC0, 0X3B80, 0XFB41, 0X3900, 0XF9C1, 0XF881, 0X3840,
            0X2800, 0XE8C1, 0XE981, 0X2940, 0XEB01, 0X2BC0, 0X2A80, 0XEA41,
            0XEE01, 0X2EC0, 0X2F80, 0XEF41, 0X2D00, 0XEDC1, 0XEC81, 0X2C40,
            0XE401, 0X24C0, 0X2580, 0XE541, 0X2700, 0XE7C1, 0XE681, 0X2640,
            0X2200, 0XE2C1, 0XE381, 0X2340, 0XE101, 0X21C0, 0X2080, 0XE041,
            0XA001, 0X60C0, 0X6180, 0XA141, 0X6300, 0XA3C1, 0XA281, 0X6240,
            0X6600, 0XA6C1, 0XA781, 0X6740, 0XA501, 0X65C0, 0X6480, 0XA441,
            0X6C00, 0XACC1, 0XAD81, 0X6D40, 0XAF01, 0X6FC0, 0X6E80, 0XAE41,
            0XAA01, 0X6AC0, 0X6B80, 0XAB41, 0X6900, 0XA9C1, 0XA881, 0X6840,
            0X7800, 0XB8C1, 0XB981, 0X7940, 0XBB01, 0X7BC0, 0X7A80, 0XBA41,
            0XBE01, 0X7EC0, 0X7F80, 0XBF41, 0X7D00, 0XBDC1, 0XBC81, 0X7C40,
            0XB401, 0X74C0, 0X7580, 0XB541, 0X7700, 0XB7C1, 0XB681, 0X7640,
            0X7200, 0XB2C1, 0XB381, 0X7340, 0XB101, 0X71C0, 0X7080, 0XB041,
            0X5000, 0X90C1, 0X9181, 0X5140, 0X9301, 0X53C0, 0X5280, 0X9241,
            0X9601, 0X56C0, 0X5780, 0X9741, 0X5500, 0X95C1, 0X9481, 0X5440,
            0X9C01, 0X5CC0, 0X5D80, 0X9D41, 0X5F00, 0X9FC1, 0X9E81, 0X5E40,
            0X5A00, 0X9AC1, 0X9B81, 0X5B40, 0X9901, 0X59C0, 0X5880, 0X9841,
            0X8801, 0X48C0, 0X4980, 0X8941, 0X4B00, 0X8BC1, 0X8A81, 0X4A40,
            0X4E00, 0X8EC1, 0X8F81, 0X4F40, 0X8D01, 0X4DC0, 0X4C80, 0X8C41,
            0X4400, 0X84C1, 0X8581, 0X4540, 0X8701, 0X47C0, 0X4680, 0X8641,
            0X8201, 0X42C0, 0X4380, 0X8341, 0X4100, 0X81C1, 0X8081, 0X4040
        };

        /// <summary>
        /// Converts four UInt16 values into a IEEE 64 floating point format.
        /// </summary>
        /// <param name="b3">Highest-order ushort value.</param>
        /// <param name="b2">Second-to-highest-order ushort value.</param>
        /// <param name="b1">Second-to-lowest-order ushort value.</param>
        /// <param name="b0">Lowest-order ushort value.</param>
        /// <returns>IEEE 64 floating point value.</returns>
        public static double GetDouble(ushort b3, ushort b2, ushort b1, ushort b0)
        {
            byte[] value = BitConverter.GetBytes(b0)
                .Concat(BitConverter.GetBytes(b1))
                .Concat(BitConverter.GetBytes(b2))
                .Concat(BitConverter.GetBytes(b3))
                .ToArray();
            return BitConverter.ToDouble(value, 0);
        }

        /// <summary>
        /// Converts two UInt16 values into a IEEE 32 floating point format
        /// </summary>
        /// <param name="highOrderValue">High order ushort value</param>
        /// <param name="lowOrderValue">Low order ushort value</param>
        /// <returns>IEEE 32 floating point value</returns>
        public static float GetSingle(ushort highOrderValue, ushort lowOrderValue)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(lowOrderValue).Concat(BitConverter.GetBytes(highOrderValue)).ToArray(), 0);
        }

        /// <summary>
        /// Converts two UInt16 values into a UInt32
        /// </summary>
        public static uint GetUInt32(ushort highOrderValue, ushort lowOrderValue)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(lowOrderValue).Concat(BitConverter.GetBytes(highOrderValue)).ToArray(), 0);
        }

        /// <summary>
        /// Converts an array of bytes to an ASCII byte array
        /// </summary>
        /// <param name="numbers">The byte array</param>
        /// <returns>An array of ASCII byte values</returns>
        public static byte[] GetAsciiBytes(params byte[] numbers)
        {
            return Encoding.ASCII.GetBytes(numbers.SelectMany(n => n.ToString("X2")).ToArray());
        }

        /// <summary>
        /// Converts an array of UInt16 to an ASCII byte array
        /// </summary>
        /// <param name="numbers">The ushort array</param>
        /// <returns>An array of ASCII byte values</returns>
        public static byte[] GetAsciiBytes(params ushort[] numbers)
        {
            return Encoding.ASCII.GetBytes(numbers.SelectMany(n => n.ToString("X4")).ToArray());
        }

        /// <summary>
        /// Calculate Longitudinal Redundancy Check.
        /// </summary>
        /// <param name="data">The data used in LRC</param>
        /// <returns>LRC value</returns>
        public static byte CalculateLrc(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            byte lrc = 0;
            foreach (byte b in data)
                lrc += b;

            lrc = (byte)((lrc ^ 0xFF) + 1);

            return lrc;
        }

        /// <summary>
        /// Calculate Cyclical Redundancy Check
        /// </summary>
        /// <param name="data">The data used in CRC</param>
        /// <returns>CRC value</returns>
        public static byte[] CalculateCrc(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            ushort crc = ushort.MaxValue;

            foreach (byte b in data)
            {
                byte tableIndex = (byte)(crc ^ b);
                crc >>= 8;
                crc ^= crcTable[tableIndex];
            }

            return BitConverter.GetBytes(crc);
        }
    }
}
