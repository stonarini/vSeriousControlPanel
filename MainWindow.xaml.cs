using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using vSeriousSDK;

namespace vSeriousControlPanel
{
    public partial class MainWindow : Window
    {

        private VSeriousDevice vSerious;
        private bool isActive;

        public ObservableCollection<LogEntry> Log { get; } = new ObservableCollection<LogEntry>();
        private CancellationTokenSource _readCancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            UpdateStatus(false);
            DataContext = this;

            PopulateComPorts();
            vSerious = new VSeriousDevice();
        }


        private async void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isActive)
            {
                string selectedComPort = ComPortComboBox.SelectedItem as string;
                if (!string.IsNullOrEmpty(selectedComPort))
                {
                    ConnectToDevice(selectedComPort);
                    await StartReadingAsync();
                }
            }
            else
            {
                DisconnectFromDevice();
                if (_readCancellationTokenSource != null)
                {
                    _readCancellationTokenSource.Cancel();
                    _readCancellationTokenSource.Dispose();
                    _readCancellationTokenSource = null;
                }
            }
        }

        private void ConnectToDevice(string comPort)
        {
            try
            {
                vSerious.SetCOMPort(comPort);
                vSerious.SetActive(true);
                UpdateStatus(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to connect: " + ex.Message);
            }
        }

        private void DisconnectFromDevice()
        {
            try
            {
                vSerious.SetActive(false);
                UpdateStatus(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to disconnect: " + ex.Message);
            }
        }


        private void PopulateComPorts()
        {
            string[] ports = SerialPort.GetPortNames();

            var availablePorts = new List<string>();
            for (int i = 5; i <= 10; i++)
            {
                string portName = "COM" + i;
                if (!ports.Contains(portName))
                    availablePorts.Add(portName);
            }

            ComPortComboBox.ItemsSource = availablePorts;

            if (availablePorts.Count > 0)
                ComPortComboBox.SelectedIndex = 0;
        }

        private void UpdateStatus(bool isActive)
        {
            this.isActive = isActive;
            StatusLabel.Content = isActive ? "Active" : "Inactive";
            StatusLabel.Foreground = isActive ? Brushes.Green : Brushes.Red;
            StatusIndicator.Fill = isActive ? Brushes.Green : Brushes.Red;
            ToggleButton.Content = isActive ? "Deactivate" : "Activate";
            InputTextBox.IsReadOnly = !isActive;
            ComPortComboBox.IsEnabled = !isActive;

            Log.Clear();
            if (!isActive)
            {
                Log.Add(new LogEntry
                {
                    Prefix = "vSerious not activated",
                    Message = "",
                    Color = Brushes.Red
                });
            }
        }

        private void SendData(string data)
        {
            if (isActive)
            {
                var bytes = Encoding.ASCII.GetBytes(data);
                vSerious.Write(bytes);
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string inputText = InputTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(inputText))
                {
                    Log.Add(new LogEntry
                    {
                        Prefix = "Out > ",
                        Message = inputText,
                        Color = Brushes.Blue
                    });
                    InputTextBox.Clear();
                }
                e.Handled = true;
            }
        }

        private async Task StartReadingAsync()
        {
            _readCancellationTokenSource = new CancellationTokenSource();
            var token = _readCancellationTokenSource.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Assume you have an instance of your VSeriousDevice called vSeriousDevice
                    //byte[] data = await Task.Run(() => vSerious.Read(1024), token);
                    byte[] data = await Task.Run(() => System.Text.Encoding.UTF8.GetBytes("Hello there"), token);


                    if (data != null && data.Length > 0)
                    {
                        string receivedText = Encoding.UTF8.GetString(data);

                        // Update UI on the main thread
                        Dispatcher.Invoke(() =>
                        {
                            Log.Add(new LogEntry
                            {
                                Prefix = "In > ",
                                Message = receivedText,
                                Color = Brushes.Red
                            });
                        });
                    }

                    await Task.Delay(10000);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
