using HandyControl.Controls;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = HandyControl.Controls.MessageBox;
using Window = HandyControl.Controls.Window;

namespace GameBridgeServer
{
    public static class ServerClass
    {
        public static string ipAdrease = string.Empty;
        public static string portAdrease = string.Empty;
        public static string StatusLog = string.Empty;
        private static Socket serverSocket;
        public const int SERVER_PORT = 5000;
        public const int BUFFER_SIZE = 8024;
        public const double TIMEOUT = 0.1; // in seconds
        public static bool LXinverteadY = false;
        public static bool LXinverteadX = false;
        public static bool RLXinverteadY = false;
        public static bool RLXinverteadX = false;
        public static DispatcherTimer ServerUpdater = null;

        /// <summary>
        /// 0x16 is left Joystick
        /// 0x13 is right joystick
        /// 
        /// 0x14 is right Trigger
        ///  0x15 left trigger
        /// </summary>
        // Dictionary to map received data to actions
        private static Dictionary<byte[], Action<byte[], EndPoint>> actionDictionary = new Dictionary<byte[], Action<byte[], EndPoint>>()
        {
   { new byte[] { 0x16 }, (data, endPoint) =>
        {
             if (LXinverteadX)
            {
            GamepadHandler._xbox360Controller.SetAxisValue(Xbox360Axis.LeftThumbX, (short)-ConvertToAnalog(data[1]));
            }
            else
            {
            GamepadHandler._xbox360Controller.SetAxisValue(Xbox360Axis.LeftThumbX, ConvertToAnalog(data[1]));
            }
            if (!LXinverteadY)
            {
               GamepadHandler._xbox360Controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)-ConvertToAnalog(data[2]));
            }
            else
            {
              GamepadHandler._xbox360Controller.SetAxisValue(Xbox360Axis.LeftThumbY, ConvertToAnalog(data[2]));
            }
        }
    },

   { new byte[] { 0x13 }, (data, endPoint) =>
        {
             if (RLXinverteadX)
            {
            GamepadHandler._xbox360Controller.SetAxisValue(Xbox360Axis.RightThumbX,(short)-ConvertToAnalog(data[1]));
            }
            else
            {
            GamepadHandler._xbox360Controller.SetAxisValue(Xbox360Axis.RightThumbX, ConvertToAnalog(data[1]));
            }
            if (!RLXinverteadY)
            {
               GamepadHandler._xbox360Controller.SetAxisValue(Xbox360Axis.RightThumbY, (short)-ConvertToAnalog(data[2]));
            }
            else
            {
              GamepadHandler._xbox360Controller.SetAxisValue(Xbox360Axis.RightThumbY, ConvertToAnalog(data[2]));
            }
        }
    },
       { new byte[] { 0x14, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetSliderValue(Xbox360Slider.RightTrigger, data[2]) },
       { new byte[] { 0x15, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetSliderValue(Xbox360Slider.LeftTrigger, data[2]) },

    { new byte[] { 0x00, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.A, true) },
    { new byte[] { 0x00, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.A, false) },
    { new byte[] { 0x01, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.B, true) },
    { new byte[] { 0x01, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.B, false) },
    { new byte[] { 0x02, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.X, true) },
    { new byte[] { 0x02, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.X, false) },
    { new byte[] { 0x03, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Y, true) },
    { new byte[] { 0x03, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Y, false) },
    { new byte[] { 0x04, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.LeftShoulder, true) },
    { new byte[] { 0x04, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.LeftShoulder, false) },
    { new byte[] { 0x05, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.RightShoulder, true) },
    { new byte[] { 0x05, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.RightShoulder, false) },
    { new byte[] { 0x06, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Start, true) },
    { new byte[] { 0x06, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Start, false) },
    { new byte[] { 0x07, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Back, true) },
    { new byte[] { 0x07, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Back, false) },
    { new byte[] { 0x08, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.LeftThumb, true) },
    { new byte[] { 0x08, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.LeftThumb, false) },
    { new byte[] { 0x09, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.RightThumb, true) },
    { new byte[] { 0x09, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.RightThumb, false) },
    { new byte[] { 0x10, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Guide, true) },
    { new byte[] { 0x10, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Guide, false) },
    { new byte[] { 0x11, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Down, true) },
    { new byte[] { 0x11, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Down, false) },
    { new byte[] { 0x12, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Up, true) },
    { new byte[] { 0x12, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Up, false) },
    { new byte[] { 0x17, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Right, true) },
    { new byte[] { 0x17, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Right, false) },
    { new byte[] { 0x18, 0x01 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Left, true) },
    { new byte[] { 0x18, 0x00 }, (data, endPoint) => GamepadHandler._xbox360Controller.SetButtonState(Xbox360Button.Left, false) },
    { new byte[] { 0x99, 0x99 }, (data, endPoint) => Console.WriteLine("Client Connected!!") },
    { new byte[] { 0x99, 0x88 }, (data, endPoint) => Console.WriteLine("Client Disconnected!!") }
        };

        // Define the function to convert a byte to a float value
        public static float ConvertByteToFloat(byte b)
        {
            return b / 255.0f;
        }

        public static float ByteToFloat(byte b)
        {
            return (b / 127.5f) - 1.0f;
        }

        public static short ConvertToAnalog(float value)
        {
            // Convert the value from the range [-100, 100] to the range [-1, 1]
            float normalizedValue = value / 100.0f;
            float analogValue = (normalizedValue * 2) - 1;

            // Scale the value to the range [-32767, 32767] and shift it by 32767
            short analogShortValue = (short)((analogValue * 32767));

            return analogShortValue;
        }

        public static void StartInitializationTimer()
        {
            // Create a DispatcherTimer
            ServerUpdater = new DispatcherTimer();

            // Set the interval
            ServerUpdater.Interval = TimeSpan.FromSeconds(0.1); // Adjust the interval as needed

            // Hook up the Tick event
            ServerUpdater.Tick += ServerUpdater_Tick;

            // Start the timer
            ServerUpdater.Start();
        }

        private static void ServerUpdater_Tick(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public static async Task<string> ChooseIPAddressAsync()
        {
            // Get available IP addresses
            List<IPAddress> ipAddresses = new List<IPAddress>(Dns.GetHostAddresses(Dns.GetHostName()));
            string selectedIPAddress = string.Empty;

            // Create ListBox to display IP addresses
            ListBox listBox = new ListBox();
            foreach (var ipAddress in ipAddresses)
            {
                listBox.Items.Add(ipAddress.ToString());
            }

            // Create Window to display the ListBox
            Window ipWindow = new Window
            {
                Title = "Choose IP Address",
                Width = 200,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = listBox
            };

            // Handle selection event
            listBox.SelectionChanged += (sender, e) =>
            {
                if (listBox.SelectedItem != null)
                {
                    // Retrieve the selected IP address
                    selectedIPAddress = listBox.SelectedItem.ToString();
                    MessageBox.Show($"Selected IP address: {selectedIPAddress}");

                    // Close the window
                    ipWindow.Close();
                }
            };

            // Show the window
            ipWindow.ShowDialog();

            return selectedIPAddress;
        }
        // Method to start the UDP server asynchronously
        public static async Task StartServerAsync(TextBlock ipblock, TextBlock portblock, Label statusLabel)
        {
            try
            {
                // Create a new socket for UDP communication
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                // Bind the socket to the server port
                var selectedIPAddress = await ChooseIPAddressAsync();

                var ipAddress = IPAddress.Parse(selectedIPAddress);
                var endPoint = new IPEndPoint(ipAddress, SERVER_PORT);

                serverSocket.Bind(endPoint);
                serverSocket.ReceiveTimeout = (int)(TIMEOUT * 1000);

                MessageBox.Show($"UDP server is running on port {SERVER_PORT}");

                ipAdrease = selectedIPAddress;
                portAdrease = SERVER_PORT.ToString();
                StatusLog = "Connected";
                // Update UI asynchronously
                await Application.Current.Dispatcher.InvokeAsync(() => UpdateUI(ipblock, portblock, statusLabel));

                // Handle client requests asynchronously
                await Task.Run(() => HandleClientRequestsAsync());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server Start: An error occurred: {ex.Message}");
            }
        }

        public static void UpdateUI(TextBlock ipblock, TextBlock portblock, Label statusLabel)
        {
            ipblock.Text = ServerClass.ipAdrease;
            portblock.Text = ServerClass.portAdrease;
            statusLabel.Content = ServerClass.StatusLog;
        }

        private static async Task HandleClientRequestsAsync()
        {
            try
            {
                byte[] buffer = new byte[BUFFER_SIZE];
                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

                while (true)
                {
                    SocketReceiveFromResult result = await serverSocket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, clientEndPoint);

                    int bytesRead = result.ReceivedBytes;
                    byte[] receivedData = new byte[bytesRead];
                    Array.Copy(buffer, receivedData, bytesRead);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Check if the received data corresponds to any action in the dictionary
                        foreach (var item in actionDictionary)
                        {
                            if (ByteArrayCompare(receivedData, item.Key) || CheckFirstBytes(receivedData, item.Key))
                            {
                                item.Value.Invoke(receivedData, clientEndPoint);
                                break;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Client request: An error occurred: {ex.Message}");
            }
        }


        // Method to compare byte arrays
        private static bool ByteArrayCompare(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i])
                    return false;
            }

            return true;
        }
        private static bool CheckFirstBytes(byte[] data, byte[] key)
        {
            if (data.Length < key.Length)
            {
                return false;
            }

            for (int i = 0; i < key.Length; i++)
            {
                if (data[i] != key[i])
                {
                    return false;
                }
            }

            return true;
        }

    }
}

