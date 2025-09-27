using HandyControl.Controls;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
namespace GameBridgeServer
{
    public class GamepadHandler
    {
        public static IXbox360Controller? Xbox360Controller;
        
        public static void CreatX360Instence()
        {
            try
            {
                var client = new ViGEmClient();

                // prepares a new x360
                Xbox360Controller = client.CreateXbox360Controller();

                // Subscribe to vibration feedback before connecting
                Xbox360Controller.FeedbackReceived += OnVibrationReceived;

                // brings the x360 online
                Xbox360Controller.Connect();

                //Growl.InfoGlobal("Xbox 360 controller created and connected successfully!");
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal("Error in GamepadInctencer: " + ex.Message);
            }
        }

       // Keep track of whether vibration is currently active
private static bool _isVibrating;

private static void OnVibrationReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
{
    try
    {
        byte leftMotor = e.LargeMotor;
        byte rightMotor = e.SmallMotor;

        Console.WriteLine($@"Vibration received - Left: {leftMotor}, Right: {rightMotor}");

        if (leftMotor > 0 || rightMotor > 0)
        {
            // Only send if currently not vibrating or values changed
            if (!_isVibrating)
            {
                ServerClass.SendVibrationToClients(leftMotor, rightMotor);
                _isVibrating = true;
            }
            else
            {
                // Optional: send updated non-zero values if motors changed
                ServerClass.SendVibrationToClients(leftMotor, rightMotor);
            }
        }
        else
        {
            // Send zero only once when vibration stops
            if (_isVibrating)
            {
                ServerClass.SendVibrationToClients(0, 0);
                _isVibrating = false;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($@"Error handling vibration: {ex.Message}");
    }
}


        public static void DisconnectController()
        {
            try
            {
                if (Xbox360Controller != null)
                {
                    Xbox360Controller.Disconnect();
                    Xbox360Controller = null;
                    Growl.InfoGlobal("Xbox 360 controller disconnected");
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal("Error disconnecting controller: " + ex.Message);
            }
        }
    }
}