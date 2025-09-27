using HandyControl.Controls;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using GameBridgeServer.Properties;
using MessageBox = HandyControl.Controls.MessageBox;

namespace GameBridgeServer
{
    public static class ServerClass
    {
        public static string IpAdrease = string.Empty;
        private static Socket? _serverSocket;
        private static CancellationTokenSource? _cancellationTokenSource;
        
        public const int BUFFER_SIZE = 512;
        public const double TIMEOUT = 1; // in seconds
        
        private static readonly Dictionary<byte[], Action<byte[], EndPoint>> actionDictionary = new Dictionary<byte[], Action<byte[], EndPoint>>()
        {
            { new byte[] { 0x16 }, (data, endPoint) =>
                {
                    if (Properties.Settings_Designer.Default.LeftStickInvertX)
                    {
                        GamepadHandler.Xbox360Controller.SetAxisValue(Xbox360Axis.LeftThumbX, (short)-ConvertToAnalog(data[1]));
                    }
                    else
                    {
                        GamepadHandler.Xbox360Controller.SetAxisValue(Xbox360Axis.LeftThumbX, ConvertToAnalog(data[1]));
                    }
                    if (!Properties.Settings_Designer.Default.LeftStickInvertY)
                    {
                        GamepadHandler.Xbox360Controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)-ConvertToAnalog(data[2]));
                    }
                    else
                    {
                        GamepadHandler.Xbox360Controller.SetAxisValue(Xbox360Axis.LeftThumbY, ConvertToAnalog(data[2]));
                    }
                }
            },
            // ... (other dictionary entries remain the same for brevity)
        };

        // Simple single client tracking
        private static IPAddress connectedClientIP = null;
        private static DateTime lastClientActivity = DateTime.MinValue;
        private static readonly TimeSpan CLIENT_TIMEOUT = TimeSpan.FromSeconds(10);

        // Conversion methods (unchanged)
        public static float ConvertByteToFloat(byte b) => b / 255.0f;
        public static float ByteToFloat(byte b) => (b / 127.5f) - 1.0f;
        public static short ConvertToAnalog(float value)
        {
            float normalizedValue = value / 100.0f;
            float analogValue = (normalizedValue * 2) - 1;
            return (short)(analogValue * 32767);
        }

        public static async Task StartServerAsync(TextBlock ipblock, string selectedIPAddress)
        {
            try
            {
                // Validation (unchanged)
                if (ipblock == null)
                {
                    MessageBox.Show("Error: ipblock is null");
                    return;
                }
                if (string.IsNullOrWhiteSpace(selectedIPAddress))
                {
                    MessageBox.Show("Error: selectedIPAddress is null or empty");
                    return;
                }
                if (Application.Current?.Dispatcher == null)
                {
                    MessageBox.Show("Error: Application.Current.Dispatcher is null");
                    return;
                }

                // Stop any existing server first
                await StopServer();

                // Create cancellation token source for graceful shutdown
                _cancellationTokenSource = new CancellationTokenSource();

                _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                if (!IPAddress.TryParse(selectedIPAddress, out IPAddress ipAddress))
                {
                    MessageBox.Show($"Error: Failed to parse IP address '{selectedIPAddress}'");
                    return;
                }

                var endPoint = new IPEndPoint(ipAddress, Settings_Designer.Default.ServerPortSetting);

                _serverSocket.Bind(endPoint);
                _serverSocket.ReceiveTimeout = (int)(TIMEOUT * 1000);
                
                IpAdrease = selectedIPAddress;

                await Application.Current.Dispatcher.InvokeAsync(() => UpdateUI(ipblock));
                
                // Start handling requests with cancellation support
                _ = Task.Run(() => HandleClientRequestsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server Start: An error occurred: {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
                await StopServer(); // Cleanup on error
            }
        }

        public static void UpdateUI(TextBlock ipblock)
        {
            ipblock.Text = IpAdrease;
        }

        private static async Task HandleClientRequestsAsync(CancellationToken cancellationToken)
        {
            try
            {
                byte[] buffer = new byte[BUFFER_SIZE];
                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check if socket is still valid
                        if (_serverSocket == null || !_serverSocket.IsBound)
                            break;

                        // Use cancellation token with socket operation
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        cts.CancelAfter(TimeSpan.FromSeconds(1)); // 1 second timeout for responsive shutdown

                        SocketReceiveFromResult result = await _serverSocket.ReceiveFromAsync(
                            new ArraySegment<byte>(buffer), 
                            SocketFlags.None, 
                            clientEndPoint,
                            cts.Token);

                        int bytesRead = result.ReceivedBytes;

                        if (bytesRead > 0)
                        {
                            byte[] receivedData = new byte[bytesRead];
                            Array.Copy(buffer, receivedData, bytesRead);

                            await ProcessClientDataAsync(receivedData, result.RemoteEndPoint);
                        }
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        // Timeout is expected, continue listening
                        continue;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                    {
                        // Socket operation was aborted (usually during shutdown)
                        Console.WriteLine("Socket operation aborted - server shutting down");
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Socket was disposed, break out of loop
                        Console.WriteLine("Socket was disposed - server shutting down");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested, break out of loop
                        Console.WriteLine("Socket operation cancelled - server shutting down");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Server is being shut down gracefully
                Console.WriteLine("Server shutdown requested");
            }
            catch (Exception ex)
            {
                // Only show error if it's not a shutdown-related exception
                if (!cancellationToken.IsCancellationRequested)
                {
                    Growl.ErrorGlobal($"Client request: An error occurred: {ex.Message}");
                }
            }
        }

        private static async Task ProcessClientDataAsync(byte[] receivedData, EndPoint clientEndPoint)
        {
            IPAddress clientIP = ((IPEndPoint)clientEndPoint).Address;

            // Track the client (any packet from client counts as activity)
            if (connectedClientIP == null || !connectedClientIP.Equals(clientIP))
            {
                connectedClientIP = clientIP;
                Console.WriteLine($@"Client registered: {clientIP}");
            }
            
            // Update last activity time
            lastClientActivity = DateTime.Now;

            // Handle explicit connection signal
            if (ByteArrayCompare(receivedData, new byte[] { 0x99, 0x99 }))
            {
                Console.WriteLine(@"Client Connected!!");
                return;
            }
            
            // Handle explicit disconnect
            if (ByteArrayCompare(receivedData, new byte[] { 0x99, 0x88 }))
            {
                connectedClientIP = null;
                Console.WriteLine(@"Client Disconnected!!");
                return;
            }

            // Process gamepad commands
            foreach (var item in actionDictionary)
            {
                if (ByteArrayCompare(receivedData, item.Key) || CheckFirstBytes(receivedData, item.Key))
                {
                    item.Value.Invoke(receivedData, clientEndPoint);
                    return;
                }
            }
        }

        public static async void SendVibrationToClients(byte leftMotor, byte rightMotor)
        {
            // Check if client is still active
            if (connectedClientIP == null || DateTime.Now - lastClientActivity > CLIENT_TIMEOUT)
            {
                Console.WriteLine($@"No active client for vibration (last activity: {lastClientActivity})");
                connectedClientIP = null;
                return;
            }

            byte[] vibrationPacket = { 0xAA, leftMotor, rightMotor };
            Console.WriteLine($@"Sending vibration to client {connectedClientIP}: Left={leftMotor}, Right={rightMotor}");

            try
            {
                using var vibrationSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var vibrationEndPoint = new IPEndPoint(connectedClientIP, Settings_Designer.Default.ServerPortSetting + 1);
                await vibrationSocket.SendToAsync(new ArraySegment<byte>(vibrationPacket), SocketFlags.None, vibrationEndPoint);
                Console.WriteLine($@"Vibration sent to {connectedClientIP}:{Settings_Designer.Default.ServerPortSetting + 1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Failed to send vibration: {ex.Message}");
            }
        }

        public static int GetConnectedClientCount()
        {
            if (connectedClientIP != null && DateTime.Now - lastClientActivity <= CLIENT_TIMEOUT)
                return 1;
            else
            {
                connectedClientIP = null;
                return 0;
            }
        }

        // Improved disposal with proper cleanup
        public static async Task StopServer()
        {
            try
            {
                // Signal cancellation first
                _cancellationTokenSource?.Cancel();

                // Give some time for the async operations to complete
                await Task.Delay(100);

                // Close and dispose socket
                if (_serverSocket != null)
                {
                    try
                    {
                        // For UDP sockets, we don't need to call Shutdown
                        _serverSocket.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($@"Error during socket close: {ex.Message}");
                    }
                    finally
                    {
                        _serverSocket.Dispose();
                        _serverSocket = null;
                    }
                }

                // Clean up cancellation token
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                // Reset client state
                connectedClientIP = null;
                lastClientActivity = DateTime.MinValue;
                IpAdrease = string.Empty;

                Console.WriteLine(@"Server stopped and cleaned up");
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Error stopping server: {ex.Message}");
            }
        }

        // Helper methods (unchanged)
        private static bool ByteArrayCompare(byte[] a1, byte[] a2) => a1.SequenceEqual(a2);

        private static bool CheckFirstBytes(byte[] data, byte[] key)
        {
            if (data.Length < key.Length)
                return false;

            for (int i = 0; i < key.Length; i++)
            {
                if (data[i] != key[i])
                    return false;
            }
            return true;
        }
    }
}