using HandyControl.Controls;
using HandyControl.Themes;
using HandyControl.Tools.Extension;
using Nefarius.ViGEm.Client.Targets;
using QRCoder;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Image = System.Windows.Controls.Image;
using MessageBox = HandyControl.Controls.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using HandyControl.Themes;
using System.Drawing.Imaging;
using Color = System.Drawing.Color;
using System.Net;
using Window = HandyControl.Controls.Window;

namespace GameBridgeServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : HandyControl.Controls.Window
    {

        public MainWindow()
        {
            InitializeComponent();
            GenerateQr("0.0.000");

        }

        private async void StartServerBTN_Click(object sender, RoutedEventArgs e)
        {
            string ip = await ChooseIPAddressAsync();
            GenerateQr(ip);
            GamepadHandler.CreatX360Instence();

            await ServerClass.StartServerAsync(IPATXT, PORTTxt, StatusTXT, ip);


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
            HandyControl.Controls.Window ipWindow = new Window
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

        private async void StopServerBTN_Click(object sender, RoutedEventArgs e)
        {

        }

     

        private async void QrBTN_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Popup popup = new Popup()
                {
                    Width = 350,
                    Height = 350,
                    Placement = PlacementMode.Right,
                    PlacementTarget = Application.Current.MainWindow,
                    AllowsTransparency = true,
                    ToolTip = "Right-Click to close",

                };

                Border qrImage = new Border()
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    CornerRadius = new CornerRadius(10)
                };

                popup.Child = qrImage;
                popup.IsOpen = true; // Open the Popup

                popup.MouseDown += (sender, e) =>
                {
                    // Close the popup when clicked
                    popup.IsOpen = false;
                };

            }
            catch (Exception ex)
            {
                MessageBox.Fatal(ex.Message);
                throw;
            }
        }

        public async Task GenerateQr(string ip)
        {
            try
            {
                string textToEncode = $"{ip}:5000";
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(textToEncode, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);

                // Get the QR code bitmap
                Bitmap qrCodeImage = qrCode.GetGraphic(20);

                // Invert the colors of the QR code bitmap
               // InvertColors(qrCodeImage);

                // Convert the QR code bitmap to PNG format
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    qrCodeImage.Save(memoryStream, ImageFormat.Png);
                    memoryStream.Position = 0;

                    // Create a BitmapImage from the MemoryStream
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memoryStream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    // Convert BitmapImage to BitmapFrame
                    BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapImage);

                    // Assign BitmapFrame to the ImageSource property
                    QrImg.Source = bitmapFrame;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InvertColors(Bitmap bitmap)
        {
            // Iterate through each pixel in the bitmap
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    // Get the color of the current pixel
                    Color pixelColor = bitmap.GetPixel(x, y);

                    // Invert the color
                    Color invertedColor = Color.FromArgb(pixelColor.A, 255 - pixelColor.R, 255 - pixelColor.G, 255 - pixelColor.B);

                    // Set the color of the current pixel to the inverted color
                    bitmap.SetPixel(x, y, invertedColor);
                }
            }
        }
     

      
    }
}