using QRCoder;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = HandyControl.Controls.MessageBox;
using System.Drawing.Imaging;
using Color = System.Drawing.Color;
using System.Net;
using System.Windows.Threading;
using GameBridgeServer.Properties;
using ScrollViewer = HandyControl.Controls.ScrollViewer;

namespace GameBridgeServer
{
    public partial class MainWindow
    {
        private const int LOG_TIMER_INTERVAL_MS = 500;
        private const int MAX_LOG_LINES = 1000;
        private const int MIN_PORT = 1000;
        private const int MAX_PORT = 9999;
        private const int QR_CODE_PIXEL_SIZE = 20;
        private const int INTERFACE_NAME_MAX_LENGTH = 20;

        private DispatcherTimer _logTimer;
        private bool _isServerRunning;
        private readonly StringBuilder _logBuffer = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeUi();
            SetupTimers();
        }

        private void InitializeUi()
        {
            PopulateNetworkInterfaces();
            
            ServerPortTextBox.Text = "5000";
            UpdateServerStatus(false);
            
            BindSliderEvents();
            GenerateInitialQrCode();
            AddLog("GameBridge Server initialized. Ready to start.");
        }

        private void BindSliderEvents()
        {
            LeftMotorSlider.ValueChanged += (_, _) => LeftMotorValue.Text = ((int)LeftMotorSlider.Value).ToString();
            RightMotorSlider.ValueChanged += (_, _) => RightMotorValue.Text = ((int)RightMotorSlider.Value).ToString();
        }

        private void GenerateInitialQrCode()
        {
            _ = GenerateQrAsync("Not Started");
        }

        private void SetupTimers()
        {
            _logTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(LOG_TIMER_INTERVAL_MS)
            };
            _logTimer.Tick += UpdateLogs!;
            _logTimer.Start();
        }

        private void PopulateNetworkInterfaces()
        {
            NetworkInterfaceCombo.Items.Clear();
            
            try
            {
                var interfaces = GetActiveNetworkInterfaces();

                foreach (var item in interfaces)
                {
                    var comboItem = new ComboBoxItem 
                    { 
                        Content = item.Display, 
                        Tag = item.Address 
                    };
                    NetworkInterfaceCombo.Items.Add(comboItem);
                }

                SelectFirstInterface();
            }
            catch (Exception ex)
            {
                AddLog($"Error loading network interfaces: {ex.Message}");
            }
        }

        private List<dynamic> GetActiveNetworkInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsValidInterface)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(addr => new
                {
                    Display = $"{addr.Address} ({GetInterfaceName(addr.Address)})",
                    Address = addr.Address.ToString()
                })
                .Cast<dynamic>()
                .ToList();
        }

        private static bool IsValidInterface(NetworkInterface ni)
        {
            return ni.OperationalStatus == OperationalStatus.Up && 
                   ni.NetworkInterfaceType != NetworkInterfaceType.Loopback;
        }

        private void SelectFirstInterface()
        {
            if (NetworkInterfaceCombo.Items.Count > 0)
                NetworkInterfaceCombo.SelectedIndex = 0;
        }

        private string GetInterfaceName(IPAddress address)
        {
            try
            {
                var matchingInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(ni => HasMatchingAddress(ni, address));

                if (matchingInterface != null)
                {
                    return TruncateInterfaceName(matchingInterface.Name);
                }
            }
            catch
            {
                // Silently handle exceptions
            }

            return "Unknown";
        }

        private static bool HasMatchingAddress(NetworkInterface ni, IPAddress address)
        {
            return ni.GetIPProperties().UnicastAddresses
                .Any(a => a.Address.Equals(address));
        }

        private string TruncateInterfaceName(string name)
        {
            return name.Length > INTERFACE_NAME_MAX_LENGTH 
                ? name.Substring(0, INTERFACE_NAME_MAX_LENGTH - 3) + "..." 
                : name;
        }

        private async void StartServerBTN_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedIp = ValidateAndGetSelectedIp();
                if (selectedIp == null) return;

                await StartServerAsync(selectedIp);
            }
            catch (Exception ex)
            {
                HandleServerStartError(ex);
            }
        }

        private string? ValidateAndGetSelectedIp()
        {
            if (NetworkInterfaceCombo.SelectedItem == null)
            {
                MessageBox.Warning("Please select a network interface first.");
                return null;
            }

            if (NetworkInterfaceCombo.SelectedItem is not ComboBoxItem selectedItem)
            {
                MessageBox.Warning("Issue with the selected network interface.");
                return null;
            }

            return selectedItem.Tag.ToString();
        }

        private async Task StartServerAsync(string selectedIp)
        {
            AddLog($"Starting server on {selectedIp}:{ServerPortTextBox.Text}...");
            
            UpdateUIForServerStart();
            LogPortChangeIfNeeded();
            
            InitializeGamepad();
            await GenerateQrAsync($"{selectedIp}:{Settings_Designer.Default.ServerPortSetting}");
            await ServerClass.StartServerAsync(IPAddressText, selectedIp);

            _isServerRunning = true;
            UpdateServerStatus(true);
            AddLog("Server started successfully!");
        }

        private void UpdateUIForServerStart()
        {
            StartServerBTN.IsEnabled = false;
            StopServerBTN.IsEnabled = true;
            NetworkInterfaceCombo.IsEnabled = false;
            ServerPortTextBox.IsEnabled = false;
        }

        private void LogPortChangeIfNeeded()
        {
            if (int.TryParse(ServerPortTextBox.Text, out int port) && 
                port != Settings_Designer.Default.ServerPortSetting)
            {
                AddLog($"Note: Port changed from {Settings_Designer.Default.ServerPortSetting} to {port}. Restart may be required.");
            }
        }

        private void InitializeGamepad()
        {
            GamepadHandler.CreatX360Instence();
            AddLog("Virtual Xbox 360 controller created.");
        }

        private void HandleServerStartError(Exception ex)
        {
            AddLog($"Failed to start server: {ex.Message}\n{ex.Source}\n{ex.StackTrace}");
            MessageBox.Error($"Server start failed: {ex.Message}");
            UpdateServerStatus(false);
        }

        private async void StopServerBTN_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await StopServerAsync();
            }
            catch (Exception ex)
            {
                AddLog($"Error stopping server: {ex.Message}");
                MessageBox.Error($"Error stopping server: {ex.Message}");
            }
        }

        private async Task StopServerAsync()
        {
            AddLog("Stopping server...");
            
            await ServerClass.StopServer();
            GamepadHandler.DisconnectController();
            
            _isServerRunning = false;
            UpdateServerStatus(false);
            AddLog("Server stopped.");

            ResetUIAfterServerStop();
            await GenerateQrAsync("Not Started");
        }

        private void ResetUIAfterServerStop()
        {
            StartServerBTN.IsEnabled = true;
            StopServerBTN.IsEnabled = false;
            NetworkInterfaceCombo.IsEnabled = true;
            ServerPortTextBox.IsEnabled = true;
            
            IPAddressText.Text = "Not Started";
            ClientCountText.Text = "0";
        }

        private void TestVibrationBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SendVibrationTest();
            }
            catch (Exception ex)
            {
                AddLog($"Vibration test failed: {ex.Message}");
                MessageBox.Warning($"Vibration test failed: {ex.Message}");
            }
        }

        private void SendVibrationTest()
        {
            byte leftMotor = (byte)LeftMotorSlider.Value;
            byte rightMotor = (byte)RightMotorSlider.Value;
            
            ServerClass.SendVibrationToClients(leftMotor, rightMotor);
            AddLog($"Vibration test sent - Left: {leftMotor}, Right: {rightMotor}");
        }

        private void StopVibrationBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopVibration();
            }
            catch (Exception ex)
            {
                AddLog($"Error stopping vibration: {ex.Message}");
            }
        }

        private void StopVibration()
        {
            ServerClass.SendVibrationToClients(0, 0);
            AddLog("Vibration stopped");
            
            ResetVibrationSliders();
        }

        private void ResetVibrationSliders()
        {
            LeftMotorSlider.Value = 0;
            RightMotorSlider.Value = 0;
        }

        private void ClearLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            ClearLogs();
        }

        private void ClearLogs()
        {
            LogsTextBlock.Text = "";
            _logBuffer.Clear();
            AddLog("Logs cleared.");
        }

        private void UpdateLogs(object sender, EventArgs e)
        {
            if (ShouldAutoScroll())
            {
                AutoScrollLogs();
            }
        }

        private bool ShouldAutoScroll()
        {
            return AutoScrollLogsCheckBox?.IsChecked == true && 
                   !string.IsNullOrEmpty(LogsTextBlock.Text);
        }

        private void AutoScrollLogs()
        {
            var scrollViewer = FindVisualParent<ScrollViewer>(LogsTextBlock);
            scrollViewer?.ScrollToEnd();
        }

        private void UpdateServerStatus(bool isRunning)
        {
            if (isRunning)
            {
                SetRunningStatus();
            }
            else
            {
                SetStoppedStatus();
            }
        }

        private void SetRunningStatus()
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
            StatusText.Text = "Running";
            StatusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
        }

        private void SetStoppedStatus()
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Gray);
            StatusText.Text = "Stopped";
            StatusText.Foreground = new SolidColorBrush(Colors.Gray);
        }

        public void AddLog(string message)
        {
            string logEntry = CreateLogEntry(message);
            
            Dispatcher.BeginInvoke(() =>
            {
                AppendLogEntry(logEntry);
                TrimLogsIfNeeded();
            });
        }

        private string CreateLogEntry(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            return $"[{timestamp}] {message}\n";
        }

        private void AppendLogEntry(string logEntry)
        {
            LogsTextBlock.Text += logEntry;
        }

        private void TrimLogsIfNeeded()
        {
            var lines = LogsTextBlock.Text.Split('\n');
            if (lines.Length > MAX_LOG_LINES)
            {
                LogsTextBlock.Text = string.Join("\n", lines.TakeLast(MAX_LOG_LINES));
            }
        }

        public async Task GenerateQrAsync(string connectionInfo)
        {
            try
            {
                if (connectionInfo == "Not Started")
                {
                    ClearQrCode();
                    return;
                }

                await Task.Run(() => GenerateQrCode(connectionInfo));
                AddLog($"QR Code generated for: {connectionInfo}");
            }
            catch (Exception ex)
            {
                HandleQrGenerationError(ex);
            }
        }

        private void ClearQrCode()
        {
            QrCodeImage.Source = null;
        }

        private void GenerateQrCode(string connectionInfo)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(connectionInfo, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrCodeData);
            using var qrCodeImage = qrCode.GetGraphic(QR_CODE_PIXEL_SIZE, Color.Black, Color.White, true);

            var bitmapImage = ConvertToBitmapImage(qrCodeImage);
            
            Dispatcher.BeginInvoke(() =>
            {
                QrCodeImage.Source = BitmapFrame.Create(bitmapImage);
            });
        }

        private BitmapImage ConvertToBitmapImage(Bitmap bitmap)
        {
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Png);
            memoryStream.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memoryStream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            
            return bitmapImage;
        }

        private void HandleQrGenerationError(Exception ex)
        {
            AddLog($"QR Code generation failed: {ex.Message}");
            MessageBox.Error($"QR Code generation failed: {ex.Message}");
        }

        private T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;

            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            
            return parentObject is T parent ? parent : FindVisualParent<T>(parentObject);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                CleanupResources();
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Cleanup error: {ex.Message}");
            }
            
            base.OnClosed(e);
        }

        private void CleanupResources()
        {
            _logTimer?.Stop();
            
            if (_isServerRunning)
            {
                _ = ServerClass.StopServer();
                GamepadHandler.DisconnectController();
            }
        }

        private void ServerPortTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateServerPort();
        }

        private void ValidateServerPort()
        {
            string text = ServerPortTextBox.Text;

            if (HasInvalidCharacters(text) || !IsValidPort(text))
            {
                ShowPortWarning();
                return;
            }

            HidePortWarning();
            SavePortSetting(text);
        }

        private bool HasInvalidCharacters(string text)
        {
            return text.Any(c => !char.IsDigit(c));
        }

        private bool IsValidPort(string text)
        {
            return int.TryParse(text, out int port) && IsPortInValidRange(port);
        }

        private bool IsPortInValidRange(int port)
        {
            return port >= MIN_PORT && port <= MAX_PORT;
        }

        private void ShowPortWarning()
        {
            ServerPortTextBoxWarrning.Visibility = Visibility.Visible;
        }

        private void HidePortWarning()
        {
            ServerPortTextBoxWarrning.Visibility = Visibility.Collapsed;
        }

        private void SavePortSetting(string text)
        {
            if (int.TryParse(text, out int port))
            {
                Settings_Designer.Default.ServerPortSetting = port;
                Settings_Designer.Default.Save();
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            ServerPortTextBox.TextChanged += ServerPortTextBox_OnTextChanged;
        }

        private void InvertChecker_OnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox) return;

            UpdateSettingFromCheckBox(checkBox);
        }

        private void UpdateSettingFromCheckBox(CheckBox checkBox)
        {
            bool isChecked = checkBox.IsChecked == true;
            
            var property = GetSettingsProperty(checkBox.Name);
            if (IsValidBooleanProperty(property))
            {
                property.SetValue(Settings_Designer.Default, isChecked);
                Settings_Designer.Default.Save();
            }
        }

        private System.Reflection.PropertyInfo? GetSettingsProperty(string propertyName)
        {
            return typeof(Settings_Designer).GetProperty(propertyName);
        }

        private bool IsValidBooleanProperty(System.Reflection.PropertyInfo? property)
        {
            return property != null && property.PropertyType == typeof(bool);
        }
    }
}