using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFServer
{
    partial class Server
    {
        void OnPlayerChat(string message, SteamId id)
        {

            WFPlayer sender = AllPlayers.Find(p => p.SteamId == id);
            Console.WriteLine($"{sender.FisherName}: {message}");

            foreach (PluginInstance plugin in loadedPlugins)
            {
                plugin.plugin.onChatMessage(sender, message);
            }
        }
    }
}
