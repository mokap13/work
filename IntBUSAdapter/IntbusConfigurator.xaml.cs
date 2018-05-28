using IntBUSAdapter.IntbusInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing;
using System.Windows.Forms;

namespace IntBUSAdapter
{
    /// <summary>
    /// Логика взаимодействия для IntbusConfigurator.xaml
    /// </summary>
    public partial class IntbusConfigurator : Window
    {
        public ObservableCollection<IntbusDevice> IntbusDevices { get; set; }
        public IntbusDevice IntbusDeviceCloneBuffer { get; set; }
        public ObservableCollection<IntbusInterface> IntbusInterfaces { get; set; }
        public IntbusInterface IntbusInterface { get; set; }
        public string IntbusName { get; set; }

        public int IntbusAddress { get; set; }
        public IntbusConfigurator()
        {
            InitializeComponent();
            this.DataContext = this;
            
            IntbusInterfaces = new ObservableCollection<IntbusInterface>
            {
                new UART0(),
                new FM(),
                new I2C(),
                new OWI(),
                new SPI()
            };
            IntbusInterface = IntbusInterfaces.First();
            IntbusDevice A = new IntbusDevice(new OWI(), 1)
            {
                Name = "Датчик давления"
            };
            IntbusDevice B = new IntbusDevice(new SPI(), 1)
            {
                Name = "БП"
            };
            B.AddIntbusDevice(A);
            IntbusDevice C = new IntbusDevice(new UART0(), 1)
            {
                Name = "О-модем"
            };
            C.AddIntbusDevice(B);
            C.AddIntbusDevice(A);
            //IntbusDevices = new ObservableCollection<IntbusDevice>
            //{
            //    C
            //};
        }

        private void CommandCopy_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if(TreeView_IntbusDevices.SelectedItem != null)
                e.CanExecute = true;
        }
        
        private void CommandCopy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IntbusDevice intbusDevice = TreeView_IntbusDevices.SelectedItem as IntbusDevice;
            IntbusDeviceCloneBuffer = intbusDevice.Clone() as IntbusDevice;
            var a = IntbusDeviceCloneBuffer;
        }

        private void CommandPaste_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (IntbusDeviceCloneBuffer != null)
                e.CanExecute = true;
        }

        private void CommandPaste_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IntbusDevice intbusDevice = TreeView_IntbusDevices.SelectedItem as IntbusDevice;

            IntbusDevice copiedIntbusDevice = IntbusDeviceCloneBuffer;
            intbusDevice.AddIntbusDevice(copiedIntbusDevice);
        }

        private void CommandCut_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (TreeView_IntbusDevices.SelectedItem != null)
                e.CanExecute = true;
        }

        private void CommandCut_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IntbusDevice intbusDevice = TreeView_IntbusDevices.SelectedItem as IntbusDevice;

            IntbusDevice copiedIntbusDevice = IntbusDeviceCloneBuffer;
            intbusDevice.AddIntbusDevice(copiedIntbusDevice);
            IntbusDeviceCloneBuffer = null;
        }

        private void CommandNew_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            string addressText = TextBox_IntbusAddress.Text;
            
            if (int.TryParse(addressText, out int address))
                if(address < 32 && address > 0)
                    e.CanExecute = true;
        }

        private void CommandNew_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if(TreeView_IntbusDevices.SelectedItem == null)
            {
                IntbusDevices.Add(new IntbusDevice(IntbusInterface, IntbusAddress) { Name = IntbusName });
            }
            else
            {
                IntbusDevice intbusDevice = TreeView_IntbusDevices.SelectedItem as IntbusDevice;
                intbusDevice.AddIntbusDevice(
                    new IntbusDevice(
                        IntbusInterface.Clone() as IntbusInterface, 
                        IntbusAddress) { Name = IntbusName }
                );
            }
        }

        private void CommandDelete_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (TreeView_IntbusDevices.SelectedItem != null)
                e.CanExecute = true;
        }

        private void CommandDelete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IntbusDevice intbusDevice = (TreeView_IntbusDevices.SelectedItem as IntbusDevice);
            intbusDevice.Dispose();
            //intbusDevice.Dispose();
            //IntbusDevices.Remove(intbusDevice);
            //intbusDevice = null;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // создаем объект BinaryFormatter
            BinaryFormatter formatter = new BinaryFormatter();
            // получаем поток, куда будем записывать сериализованный объект
            using (FileStream fs = new FileStream("deviceConfig.dat", FileMode.OpenOrCreate))
            {
                formatter.Serialize(fs, IntbusDevices);
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            // создаем объект BinaryFormatter
            BinaryFormatter formatter = new BinaryFormatter();
            // десериализация из файла people.dat
            try
            {
                using (FileStream fs = new FileStream("deviceConfig.dat", FileMode.OpenOrCreate))
                {
                    IntbusDevices = (ObservableCollection<IntbusDevice>)formatter.Deserialize(fs);
                }
            }
            catch (Exception)
            {

                
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = !e.Text.Any(x => Char.IsDigit(x) || ':'.Equals(x));
    }
}
