using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFSermver
{
    partial class Server
    {
        Dictionary<string, object> readPacket(byte[] packetBytes)
        {
            return (new GodotPacketDeserializer(packetBytes)).readPacket();
        }

        byte[] writePacket(Dictionary<string, object> packet)
        {
            byte[] godotBytes = GodotWriter.WriteGodotPacket(packet);
            return GzipHelper.CompressGzip(godotBytes);
        }

        void sendPacketToPlayers(Dictionary<string, object> packet)
        {
            byte[] packetBytes = writePacket(packet);
            // get all players in the lobby
            foreach (Friend member in gameLobby.Members)
            {
                if (member.Id == SteamClient.SteamId.Value) continue;
                SteamNetworking.SendP2PPacket(member.Id, packetBytes, nChannel: 2);
            }
        }
    }
}
