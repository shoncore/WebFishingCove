using Cove.Server.Actor;

namespace Cove.Server.Plugins
{
  public class CovePlugin(CoveServer parent)
  {
    private CoveServer parentServer = parent;

    // triggered when the plugin is started
    public virtual void OnInit() { }
    // triggerd 12/s
    public virtual void OnUpdate() { }
    // triggered when a player speaks in anyway (exluding / commands)
    public virtual void OnChatMessage(WFPlayer sender, string message) { }
    // triggerd when a player enters the server
    public virtual void OnPlayerJoin(WFPlayer player) { }
    // triggered when a player leaves the server
    public virtual void OnPlayerLeave(WFPlayer player) { }

    public WFPlayer[] GetAllPlayers()
    {
      return [.. parentServer.AllPlayers];
    }

    public void SendPlayerChatMessage(WFPlayer receiver, string message, string hexColor = "ffffff")
    {
      // remove a # incase its given
      parentServer.MessagePlayer(message, receiver.SteamId, hexColor.Replace("#", ""));
    }

    public void SendGlobalChatMessage(string message, string hexColor = "ffffff")
    {
      parentServer.MessageGlobal(message, hexColor.Replace("#", ""));
    }

    public WFActor[] GetAllServerActors()
    {
      return [.. parentServer.ServerOwnedInstances];
    }

    public WFActor? GetActorFromID(int id)
    {
      return parentServer.ServerOwnedInstances.Find(a => a.InstanceID == id);
    }

    // please make sure you use the correct actorname or the game freaks out!
    public WFActor SpawnServerActor(string actorType)
    {
      return parentServer.SpawnGenericActor(actorType);
    }

    public void RemoveServerActor(WFActor actor)
    {
      parentServer.RemoveServerActor(actor);
    }

    // i on god dont know what this dose to the actual actor but it works in game, so if you nee this its here
    public void SetServerActorZone(WFActor actor, string zoneName, int zoneOwner)
    {
      parentServer.SetActorZone(actor, zoneName, zoneOwner);
    }

    public static void KickPlayer(WFPlayer player)
    {
      CoveServer.KickPlayer(player.SteamId);
    }

    public void BanPlayer(WFPlayer player)
    {
      if (parentServer.IsPlayerBanned(player.SteamId))
      {
        parentServer.BanPlayer(player.SteamId);
      }
      else
      {
        parentServer.BanPlayer(player.SteamId, true); // save to file if they are not already in there!
      }
    }

    public void Log(string message)
    {
      parentServer.PrintPluginLog(message, this);
    }

    public bool IsPlayerAdmin(WFPlayer player)
    {
      return parentServer.IsPlayerAdmin(player.SteamId);
    }

    public static void SendPacketToPlayer(Dictionary<string, object> packet, WFPlayer player)
    {
      CoveServer.SendPacketToPlayer(packet, player.SteamId);
    }

    public void SendPacketToAll(Dictionary<string, object> packet)
    {
      parentServer.SendPacketToPlayers(packet);
    }
  }
}
