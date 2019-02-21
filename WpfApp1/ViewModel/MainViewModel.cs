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
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private Dictionary<int, IntbusDevice> modbusAddressDictionary = new Dictionary<int, IntbusDevice>();
        private IntbusDevice currentDevice;
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>

        private List<byte> intbusRecievedBuffer = new List<byte>();
        DateTime sendTime;
        private Task timeoutTask;
        private List<IntbusDevice> intbusDevices;
        private static CancellationTokenSource cancelTimeoutToken = new CancellationTokenSource();
        private CancellationToken timeoutTaskToken = cancelTimeoutToken.Token;

        public MainViewModel()
        {
            try
            {

                JObject jObj = JObject.Parse(File.ReadAllText(@"./intbus_device.json"));
                this.intbusDevices = jObj["intbusDevices"].ToObject<List<IntbusDevice>>();
                this.intbusDevices.ForEach(d => d.InitializeParents());

                foreach (IntbusDevice device in this.intbusDevices)
                    device.InitializeAddress(ref this.modbusAddressDictionary);

                this.IntPort = this.InitPort(jObj["intbusComPort"]);
                this.MbPort = this.InitPort(jObj["modbusComPort"]);

                this.WriteLog("IntBUS ���������: " + jObj["intbusComPort"].ToString());
                this.WriteLog("Modbus ���������: " + jObj["modbusComPort"].ToString());

                this.WriteLog("�������� intbus �������� � ������ �������");
                foreach (var pair in this.modbusAddressDictionary)
                {   
                    this.WriteLog($"(Device: {pair.Value.Name})  " +
                        $"(Address: {pair.Value.IntbusAddress})  (Interface: {pair.Value.Interface})  " +
                        $"(Modbus address: {pair.Key} )");
                }     
                

                this.MbPort.DataReceived += MbPort_DataReceived;
                this.IntPort.DataReceived += IntPort_DataReceived;
            }
            catch (Exception ex)
            {
                WriteLog($"{ex.Message}\n{ex.StackTrace}");
            }
        }

        private void IntPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int byteRecieved = this.IntPort.BytesToRead;

            byte[] responseBytes = new byte[byteRecieved];
            if (responseBytes.Length == 0)
                return;
            this.IntPort.Read(responseBytes, 0, byteRecieved);
            this.intbusRecievedBuffer.AddRange(responseBytes);

            if (ModbusUtility.IsValidCrc(this.intbusRecievedBuffer))
            {
                cancelTimeoutToken.Cancel();
                this.timeoutTask.Wait();
                cancelTimeoutToken.Dispose();
                cancelTimeoutToken = new CancellationTokenSource();
                this.timeoutTaskToken = cancelTimeoutToken.Token;

                IEnumerable<byte> expectedPreambule = this.currentDevice.CalculatePreambule();
                IEnumerable<byte> actualPreambule = this.intbusRecievedBuffer.Take(expectedPreambule.Count());

                if (expectedPreambule.Zip(actualPreambule,(f,s) => new { f, s }).Any(p => p.f != p.s))
                {
                    this.SerialDataIntBUSResponse("Preambule ERROR");
                    return;
                }
                
                if(this.intbusRecievedBuffer.Skip(expectedPreambule.Count()).First() != 
                    this.currentDevice.ModbusAddress)
                {
                    this.SerialDataIntBUSResponse("ModbusAddress ERROR");
                    return;
                }

                
                var receivedMbFrame = this.intbusRecievedBuffer
                    .Take(this.intbusRecievedBuffer.Count - 2)
                    .Skip(expectedPreambule.Count());
                var crc = ModbusUtility.CalculateCrc(receivedMbFrame.ToArray());
                this.intbusRecievedBuffer = receivedMbFrame
                    .ToList();
                this.intbusRecievedBuffer.AddRange(crc);

                this.SerialDataIntBUSResponse("");
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
                if(!this.modbusAddressDictionary.ContainsKey(mbAddress))
                    throw new Exception($"������ ������������� �����({mbAddress}) modbus �� ���������� " +
                        $"� Intbus ������������");

                this.currentDevice = this.modbusAddressDictionary[mbAddress];

                intbusFrame = this.currentDevice.ConvertToIntbus(receivedBytes.ToList());
                if (intbusFrame == null)
                    throw new Exception($"Exception: {this.currentDevice.Name} ConvertToIntbus: " +
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

                List<byte> currentPreambule = this.currentDevice.CalculatePreambule();
                for (int i = currentPreambule.Count; i < intbusFrame.Count; i++)
                    message += String.Format("{0:X2} ", intbusFrame[i]);
                /////////////////////////////////
                receivedBytes = intbusFrame.ToArray();
                this.IntPort.DiscardOutBuffer();
                this.IntPort.DiscardInBuffer();
                this.IntPort.Write(receivedBytes, 0, receivedBytes.Length);

                while (this.IntPort.BytesToWrite != 0)
                    ;
                this.sendTime = DateTime.Now;

                if(this.timeoutTask != null)
                {
                    cancelTimeoutToken.Cancel();
                    this.timeoutTask.Wait();
                    cancelTimeoutToken.Dispose();
                    cancelTimeoutToken = new CancellationTokenSource();
                    this.timeoutTaskToken = cancelTimeoutToken.Token;
                };
                
                this.timeoutTask = Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        if (this.timeoutTaskToken.IsCancellationRequested)
                            return;
                        if (DateTime.Now - sendTime >= TimeSpan.FromMilliseconds(this.responseTimeout))
                        {
                            byteRecieved = this.IntPort.BytesToRead;

                            byte[] responseBytes = new byte[byteRecieved];
                            
                            this.IntPort.Read(responseBytes, 0, byteRecieved);
                            this.intbusRecievedBuffer.AddRange(responseBytes);
                            if (this.intbusRecievedBuffer.Count == 0)
                            {
                                this.SerialDataIntBUSResponse("TIMEOUT, NO RESPONSE");
                            }
                            else
                            {
                                this.SerialDataIntBUSResponse("TIMEOUT, CRC ERROR");
                            }
                            
                            return;
                        }
                    }
                });
                string preambule = BitConverter.ToString(currentDevice.CalculatePreambule().ToArray()).Replace('-', ' ');
                this.WriteLog($"{this.currentDevice.Name}  TX: " +
                    $"[{preambule}] {message}");
            }
        }

        private void SerialDataIntBUSResponse(string addingMessage)
        {
            this.IntPort.DiscardInBuffer();
            this.IntPort.DiscardOutBuffer();
            List<byte> preambule = currentDevice.CalculatePreambule();
            string strPreambule = BitConverter.ToString(preambule.ToArray()).Replace('-', ' ');

            string message = BitConverter.ToString(this.intbusRecievedBuffer.ToArray()).Replace('-', ' ');

            if (this.MbPort != null && this.MbPort.IsOpen)
            {
                //������� ������ ������ ��� �����(��������� ������ �������)
                if(this.intbusRecievedBuffer.Count > preambule.Count())
                    this.intbusRecievedBuffer[preambule.Count()] = (byte)this.currentDevice.VirtualModbusAddress;
                this.MbPort.Write(this.intbusRecievedBuffer.ToArray(), 0, this.intbusRecievedBuffer.Count);
            }
            
            this.WriteLog($"{this.currentDevice.Name}  RX: [{strPreambule}] {message} {addingMessage}");

            this.intbusRecievedBuffer.Clear();
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
            this.Log += $"{DateTime.Now.TimeOfDay}: {log}\n";
        }

        public void Dispose()
        {
            this.MbPort.DataReceived -= MbPort_DataReceived;
            this.IntPort.DataReceived -= IntPort_DataReceived;
        }

        public SerialPort IntPort { get; set; }
        public SerialPort MbPort { get; set; }

        #region Commands

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
                            this.WriteLog($"IntPort {this.IntPort.PortName}: ������� ������");
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
                            this.WriteLog($"MbPort {this.MbPort.PortName}: ������� ������");
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
                            this.WriteLog($"MbPort {this.MbPort.PortName}: ������� ������");
                            this.IntPort.Close();
                            this.WriteLog($"IntPort {this.IntPort.PortName}: ������� ������");
                        }
                        catch (Exception ex)
                        {
                            this.WriteLog($"{ex.Source.ToString()}: {ex.Message}");
                        }
                    }));
            }
        }

        #endregion
    }
}