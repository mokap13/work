using IntBUSAdapter.IntbusInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace IntBUSAdapter
{
    [Serializable]
    public class IntbusDevice : ICloneable, INotifyPropertyChanged
    {
        private string name;
        //private int address;
        //private IntbusInterface intbusInterface;
        private int modbusAddress;

        private int address;

        public int Address
        {
            get { return address; }
            set { address = value; }
        }
        private IntbusInterface intbusInterface;

        public IntbusInterface IntbusInterface
        {
            get { return intbusInterface; }
            set { intbusInterface = value; }
        }


        public ObservableCollection<IntbusDevice> SlaveIntbusDevices { get; set; }
        public IntbusDevice MasterIntbusDevice { get; set; }
        public List<byte> PrefixBytes { get; set; }
        public List<byte> PostfixBytes { get; set; }
        public byte AddressWithInterface => (byte)(address | (this.IntbusInterface.Number << 5));

        public string IntbusInterfaceName => IntbusInterface.Name;
        public static Dictionary<int, IntbusDevice> modbusDeviceAddresses;
        public static Dictionary<int, IntbusDevice> ModbusDeviceAddresses
        {
            get
            {
                return modbusDeviceAddresses;
            }
        }
        public int ModbusDeviceAddress
        {
            get
            {
                return modbusAddress;
            }
            set
            {
                if (value < 248 && value > 0)
                {
                    int temp = value;
                    if (ModbusDeviceAddresses == null)
                        modbusDeviceAddresses = new Dictionary<int, IntbusDevice>();
                    if (!ModbusDeviceAddresses.ContainsKey(temp))
                    {
                        modbusAddress = temp;
                        ModbusDeviceAddresses.Add(temp, this);
                    }
                    else
                    {
                        throw new Exception("Такой модбас адрес уже используется.");
                    }
                }
                else
                {
                    throw new Exception("Адрес Модбас устройства должен быть от 1 до 247.");
                }
            }
        }

        public void AddIntbusDevice(IntbusDevice intbusDevice)
        {
            IntbusDevice clonedDevice = intbusDevice.Clone() as IntbusDevice;
            clonedDevice.MasterIntbusDevice = this;
            this.SlaveIntbusDevices.Add(clonedDevice);
        }
        public List<byte> ConvertToIntbusFrame(List<byte> modbusFrame)
        {
            IntbusDevice intbusDevice = this;
            do
            {
                modbusFrame.Insert(0, intbusDevice.AddressWithInterface);
                modbusFrame.InsertRange(0, intbusDevice.PrefixBytes);
                modbusFrame.AddRange(intbusDevice.PostfixBytes);
                intbusDevice = intbusDevice.MasterIntbusDevice;
            } while (intbusDevice != null);
            modbusFrame.RemoveRange(modbusFrame.Count - 2, 2);
            byte[] crc = ModbusUtility.CalculateCrc(modbusFrame.ToArray());
            modbusFrame.AddRange(crc.ToList());

            return modbusFrame;
        }

        public object Clone()
        {
            IntbusDevice intbusDevice = new IntbusDevice(this.IntbusInterface, this.address)
            {
                Name = this.name,
                MasterIntbusDevice = this.MasterIntbusDevice,
            };
            foreach (var slaveDevice in this.SlaveIntbusDevices)
            {
                intbusDevice.AddIntbusDevice(slaveDevice.Clone() as IntbusDevice);
            }
            return intbusDevice;
        }

        public string Name
        {
            get
            {
                return name ?? "Device";
            }
            set { name = value; }
        }
        public IntbusDevice(IntbusInterface intbusInterface, int address)
        {
            this.address = address > 32 | address < 1
                ? throw new Exception("Адрес устройства должен быть от 1 до 32")
                : address;
            this.IntbusInterface = intbusInterface;
            SlaveIntbusDevices = new ObservableCollection<IntbusDevice>();
            PrefixBytes = new List<byte>();
            PostfixBytes = new List<byte>();
        }

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyRaised(string propertyname) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
    }
}