using Steamworks;
using Cove.GodotFormat;
using Cove.Server.Utils;

namespace Cove.Server
{
    partial class CoveServer
    {
        Dictionary<string, object> readPacket(byte[] packetBytes)
        {
            return (new GodotReader(packetBytes)).readPacket();
        }

        byte[] writePacket(Dictionary<string, object> packet)
        {
            byte[] godotBytes = GodotWriter.WriteGodotPacket(packet);
            return GzipHelper.CompressGzip(godotBytes);
        }

        public void sendPacketToPlayers(Dictionary<string, object> packet)
        {
            byte[] packetBytes = writePacket(packet);
            // get all players in the lobby
            foreach (Friend member in gameLobby.Members)
            {
                if (member.Id == SteamClient.SteamId.Value) continue;
                SteamNetworking.SendP2PPacket(member.Id, packetBytes, nChannel: 2);
            }
        }

        public void sendPacketToPlayer(Dictionary<string, object> packet, SteamId id)
        {
            byte[] packetBytes = writePacket(packet);
            SteamNetworking.SendP2PPacket(id, packetBytes, nChannel: 2);
        }
    }
}
