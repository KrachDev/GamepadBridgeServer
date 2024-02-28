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

        }

        private async void StartServerBTN_Click(object sender, RoutedEventArgs e)
        {
           
            await ServerClass.StartServerAsync(IPATXT, PORTTxt, StatusTXT);
            

        }

        private async void StopServerBTN_Click(object sender, RoutedEventArgs e)
        {

        }

        private void IPACopyBTN_Click(object sender, RoutedEventArgs e)
        {
            if (IPATXT.Text != "0.0.0.000")
            {
                Clipboard.SetText(IPATXT.Text);
                HandyControl.Controls.MessageBox.Success("Ip Got copied");
            }
        }

        private void PORTCopyBTN_Click(object sender, RoutedEventArgs e)
        {
            if (PORTTxt.Text != "0000")
            {
                Clipboard.SetText(PORTTxt.Text);
                HandyControl.Controls.MessageBox.Success("PORT Got copied");
            }
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
                    ToolTip = "Right-Click to close"
                };

                Border qrImage = new Border()
                {
                    Background = new SolidColorBrush(Colors.Gray),
                    CornerRadius = new CornerRadius(10)
                };
               qrImage.Background = new ImageBrush(await GenerateQr()); // Await the method call

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

        public async Task<ImageSource> GenerateQr()
        {
            try
            {
                string textToEncode = $"{IPATXT.Text}:{PORTTxt.Text}";
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(textToEncode, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);

                Bitmap qrCodeImage = qrCode.GetGraphic(20);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    qrCodeImage.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    memoryStream.Position = 0;
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memoryStream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    return bitmapImage; // Return bitmapImage instead of qrCodeWpfImage
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private void TestBTN_Click(object sender, RoutedEventArgs e)
        {
            GamepadHandler.CreatX360Instence();
        }
        private void ClickAbtn_Click(object sender, RoutedEventArgs e)
        {
            GamepadHandler._xbox360Controller.SetButtonState(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A, true);

        }

        private void ClickAbtn_MouseDown(object sender, MouseButtonEventArgs e)
        {

          
        }

        private void ClickAbtn_MouseLeave(object sender, MouseEventArgs e)
        {
        }
    }
}