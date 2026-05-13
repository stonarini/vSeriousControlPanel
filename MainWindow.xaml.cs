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

            try
            {
                vSerious = new VSeriousDevice();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "vSerious driver not found.\n\nInstall the driver and restart this application.\n\nDetails: " + ex.Message,
                    "vSerious",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ToggleButton.IsEnabled = false;
                ComPortComboBox.IsEnabled = false;
                InputTextBox.IsReadOnly = true;
            }
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
                    byte[] data = await Task.Run(() => vSerious.Read(1024), token);

                    if (data != null && data.Length > 0)
                    {
                        string receivedText = Encoding.UTF8.GetString(data);

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
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // COM handle closed (deactivate or window close). End quietly
                // — propagating here would crash the async void caller.
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (_readCancellationTokenSource != null)
                {
                    _readCancellationTokenSource.Cancel();
                    _readCancellationTokenSource.Dispose();
                    _readCancellationTokenSource = null;
                }

                if (isActive && vSerious != null)
                {
                    try { vSerious.SetActive(false); } catch { /* best-effort */ }
                }

                vSerious?.Dispose();
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
