using GalaSoft.MvvmLight;
using System.Windows.Documents;
using WpfApp1.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Threading;
using WpfApp1.Helpers;
using GalaSoft.MvvmLight.Command;
using System.Windows.Input;

namespace WpfApp1.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private Dictionary<int, IntbusDevice> modbusAddressDictionary = new Dictionary<int, IntbusDevice>();
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            try
            {
                JObject jObj = JObject.Parse(File.ReadAllText(@"./intbus_device.json"));
                this.intbusDevices = jObj["intbusDevice"].ToObject<List<IntbusDevice>>();
                this.intbusDevices.ForEach(d => d.InitializeParents());

                foreach (IntbusDevice device in this.intbusDevices)
                    device.InitializeAddress(ref this.modbusAddressDictionary);

                this.IntPort = this.InitPort(jObj["intbusComPort"]);
                this.MbPort = this.InitPort(jObj["modbusComPort"]);

                this.WriteLog("IntBUS настройки: " + jObj["intbusComPort"].ToString());
                this.WriteLog("Modbus настройки: " + jObj["modbusComPort"].ToString());

                this.WriteLog("Привязка intbus устройсв к модбас адоесам");
                foreach (var pair in this.modbusAddressDictionary)
                {
                    this.WriteLog($"Device: {pair.Value.Name}  *  " +
                        $"Address: {pair.Value.Address}  *  Interface: {pair.Value.Interface}  *  Modbus address: {pair.Key} ");
                }     
                

                this.MbPort.DataReceived += MbPort_DataReceived;
            }
            catch (Exception ex)
            {
                WriteLog($"{ex.Message}\n{ex.StackTrace}");
            }
        }

        private void MbPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int byteRecieved = 0;
            byte[] receivedBytes = null;
            int mbAddress = 0xFFFF;
            string message = null;
            List<byte> intbusFrame;
            try
            {
                byteRecieved = (sender as SerialPort).BytesToRead;
                receivedBytes = new byte[byteRecieved];
                (sender as SerialPort).Read(receivedBytes, 0, byteRecieved);

                mbAddress = receivedBytes.First();
                IntbusDevice device;
                if(!this.modbusAddressDictionary.ContainsKey(mbAddress))
                    throw new Exception($"Ошибка запрашиваемый адрес({mbAddress}) modbus не существует " +
                        $"в Intbus пространстве");

                device = this.modbusAddressDictionary[mbAddress];

                intbusFrame = device.ConvertToIntbus(receivedBytes.ToList());
                if (intbusFrame == null)
                    throw new Exception($"Exception: {device.Name} ConvertToIntbus: " +
                        $"{BitConverter.ToString(intbusFrame.ToArray())}");
            }
            catch (Exception ex)
            {
                this.WriteLog(ex.Message);
                this.WriteLog(ex.StackTrace);
                return;
            }
            

            if (this.IntPort != null && this.IntPort.IsOpen)
            {
                message = null;
                for (int i = 0; i < intbusFrame.Count; i++)
                    message += String.Format("{0:X2} ", intbusFrame[i]);
                /////////////////////////////////
                receivedBytes = intbusFrame.ToArray();
                this.IntPort.DiscardOutBuffer();
                this.IntPort.DiscardInBuffer();
                this.IntPort.Write(receivedBytes, 0, receivedBytes.Length);

                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(this.ResponseTimeout);
                    SerialDataIntBUSResponse();
                });
                this.WriteLog("TX: " + message);
            }
        }

        private void SerialDataIntBUSResponse()
        {
            int byteRecieved = this.IntPort.BytesToRead;
            byte[] responseBytes = new byte[byteRecieved];
            if (responseBytes.Length == 0)
                return;
            this.IntPort.Read(responseBytes, 0, byteRecieved);
            this.IntPort.DiscardInBuffer();

            string message = BitConverter.ToString(responseBytes).Replace('-', ' ');
            this.WriteLog("  --> RX:" + message);

            List<byte> listBytes = responseBytes.Skip(2).ToList();
            listBytes = listBytes.Take(listBytes.Count() - 2).ToList();
            listBytes.AddRange(ModbusUtility.CalculateCrc(listBytes.ToArray()));
            responseBytes = listBytes.ToArray();

            if (this.MbPort != null && this.MbPort.IsOpen)
            {
                this.MbPort.Write(responseBytes, 0, responseBytes.Length);
            }
        }

        private SerialPort InitPort(JToken jPort)
        {
            Parity parity = (Parity)Enum.Parse(typeof(Parity), (string)jPort["parity"]);
            StopBits stopBits = (StopBits)Enum.Parse(typeof(StopBits), (string)jPort["stopBit"]);

            return new SerialPort
            {
                PortName = (string)jPort["port"],
                BaudRate = (int)jPort["baudRate"],
                Parity = parity,
                DataBits = (int)jPort["bits"],
                StopBits = stopBits,
            };
        }

        private List<IntbusDevice> intbusDevices;

        private int responseTimeout;

        public int ResponseTimeout
        {
            get { return responseTimeout; }
            set { base.Set(() => ResponseTimeout, ref responseTimeout, value); }
        }

        private string log = string.Empty;
        public string Log
        {
            get { return log; }
            set { base.Set(() => Log, ref log, value); }
        }

        public void WriteLog(string log)
        {
            this.Log += $"{DateTime.Now}: {log}\n";
        }

        public SerialPort IntPort { get; set; }
        public SerialPort MbPort { get; set; }

        private RelayCommand openIntPortCommand;
        public ICommand OpenIntPortCommand
        {
            get
            {
                return openIntPortCommand ??
                    (openIntPortCommand = new RelayCommand(() =>
                    {
                        try
                        {
                            this.IntPort.Open();
                            this.WriteLog($"IntPort {this.IntPort.PortName}: успешно открыт");
                        }
                        catch (Exception ex)
                        {
                            this.WriteLog($"IntPort {this.IntPort.PortName}: {ex.Message}");
                        }
                    }));
            }
        }

        private RelayCommand openMbPortCommand;
        public ICommand OpenMbPortCommand
        {
            get
            {
                return openMbPortCommand ??
                    (openMbPortCommand = new RelayCommand(() =>
                    {
                        try
                        {
                            this.MbPort.Open();
                            this.WriteLog($"MbPort {this.MbPort.PortName}: успешно открыт");
                        }
                        catch (Exception ex)
                        {
                            this.WriteLog($"MbPort {this.MbPort.PortName}: {ex.Message}");
                        }
                    }));
            }
        }
        private RelayCommand closePortsCommand;
        public ICommand ClosePortsCommand
        {
            get
            {
                return closePortsCommand ??
                    (closePortsCommand = new RelayCommand(() =>
                    {
                        try
                        {
                            this.MbPort.Close();
                            this.WriteLog($"MbPort {this.MbPort.PortName}: успешно закрыт");
                            this.IntPort.Close();
                            this.WriteLog($"IntPort {this.IntPort.PortName}: успешно закрыт");
                        }
                        catch (Exception ex)
                        {
                            this.WriteLog($"{ex.Source.ToString()}: {ex.Message}");
                        }
                    }));
            }
        }
    }
}