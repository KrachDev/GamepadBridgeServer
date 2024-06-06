using HandyControl.Controls;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GameBridgeServer
{
    public class GamepadHandler
    {
        public static IXbox360Controller _xbox360Controller = null;
        public static void CreatX360Instence()
        {
            try
            {

                var client = new ViGEmClient();

                // prepares a new x360
                _xbox360Controller = client.CreateXbox360Controller();

                // brings the x360 online
                _xbox360Controller.Connect();

            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal("Error in GamepadInctencer: " + ex.Message);
            }
        }
    }
}
