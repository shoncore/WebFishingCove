﻿using Steamworks;
using Cove.GodotFormat;
using Cove.Server.Utils;

namespace Cove.Server
{
  public partial class CoveServer
  {
    private static Dictionary<string, object> ReadPacket(byte[] packetBytes)
    {
      return new GodotReader(packetBytes).ReadPacket();
    }

    private static byte[] WritePacket(Dictionary<string, object> packet)
    {
      byte[] godotBytes = GodotWriter.WriteGodotPacket(packet);
      return GzipHelper.CompressGzip(godotBytes);
    }

    public void SendPacketToPlayers(Dictionary<string, object> packet)
    {
      byte[] packetBytes = WritePacket(packet);
      // get all players in the lobby
      foreach (Friend member in GameLobby.Members)
      {
        if (member.Id == SteamClient.SteamId.Value) continue;
        SteamNetworking.SendP2PPacket(member.Id, packetBytes, nChannel: 2);
      }
    }

    public static void SendPacketToPlayer(Dictionary<string, object> packet, SteamId id)
    {
      byte[] packetBytes = WritePacket(packet);
      SteamNetworking.SendP2PPacket(id, packetBytes, nChannel: 2);
    }
  }
}
