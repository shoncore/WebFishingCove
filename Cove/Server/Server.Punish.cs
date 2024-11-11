using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Steamworks;

namespace Cove.Server
{
    public partial class CoveServer
    {

        public void banPlayer(SteamId id, bool saveToFile = false)
        {
            Dictionary<string, object> banPacket = new();
            banPacket["type"] = "ban";

            sendPacketToPlayer(banPacket, id);

            if (saveToFile)
                writeToBansFile(id);
        }

        public bool isPlayerBanned(SteamId id)
        {
            string fileDir = $"{AppDomain.CurrentDomain.BaseDirectory}bans.txt";
            if (!File.Exists(fileDir))
            {
                File.Create(fileDir);
            }

            string[] fileContent = File.ReadAllLines(fileDir);
            foreach (string line in fileContent)
            {
                if (line.Contains(id.Value.ToString()))
                {
                    return true;
                }
            }

            return false;
        }

        private void writeToBansFile(SteamId id)
        {
            string fileDir = $"{AppDomain.CurrentDomain.BaseDirectory}bans.txt";
            if (!File.Exists(fileDir))
            {
                File.Create(fileDir);
            }

            File.WriteAllText(fileDir, $"\n{id.Value}");
        }

        public void kickPlayer(SteamId id)
        {
            Dictionary<string, object> kickPacket = new();
            kickPacket["type"] = "kick";

            sendPacketToPlayer(kickPacket, id);
        }

    }
}
