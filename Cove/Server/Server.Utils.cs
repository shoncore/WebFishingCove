using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using Cove.Server.Plugins;
using Cove.GodotFormat;
using Cove.Server.Actor;
using Cove.Server.Utils;

namespace Cove.Server
{
  public partial class CoveServer
  {
    /// <summary>
    /// Reads and updates the admin list from the configuration file.
    /// </summary>
    public void ReadAdmins()
    {
      var config = ConfigReader.ReadConfig("admins.cfg");
      Admins.Clear();

      foreach (var key in config.Keys)
      {
        if (config[key].Equals("true", StringComparison.OrdinalIgnoreCase))
        {
          Console.WriteLine($"Added {key} as admin!");
          Admins.Add(key);

          var player = AllPlayers.Find(p => p.SteamId.Value.ToString() == key);
          if (player != null)
          {
            MessagePlayer("You are an admin on this server!", player.SteamId);
          }
        }
      }
    }

    /// <summary>
    /// Spawns a rain cloud at a random position and notifies all players.
    /// </summary>
    public void SpawnRainCloud()
    {
      var rand = new Random();
      var instanceId = rand.Next();

      var position = new Vector3(rand.Next(-100, 150), 42f, rand.Next(-150, 100));

      var rainSpawnPacket = new Dictionary<string, object>
      {
        ["type"] = "instance_actor",
        ["params"] = new Dictionary<string, object>
        {
          ["actor_type"] = "raincloud",
          ["at"] = position,
          ["rot"] = new Vector3(0, 0, 0),
          ["zone"] = "main_zone",
          ["zone_owner"] = -1,
          ["actor_id"] = instanceId,
          ["creator_id"] = SteamClient.SteamId.Value
        }
      };

      SendPacketToPlayers(rainSpawnPacket);
      ServerOwnedInstances.Add(new RainCloud(instanceId, position));
    }

    /// <summary>
    /// Spawns a fish of the specified type at a random position.
    /// </summary>
    public WFActor SpawnFish(string fishType = "fish_spawn")
    {
      var rand = new Random();
      if (FishPoints == null || FishPoints.Count == 0)
      {
        throw new InvalidOperationException("FishPoints is null or empty.");
      }
      var position = FishPoints[rand.Next(FishPoints.Count)] + new Vector3(0, 0.08f, 0);
      var actor = SpawnGenericActor(fishType, position);

      actor.ShouldDespawn = false;
      actor.DespawnTime = fishType == "fish_spawn" ? 80 : 120; // 80 for normal fish, 120 for alien fish

      return actor;
    }

    /// <summary>
    /// Spawns a void portal at a random hidden spot.
    /// </summary>
    public WFActor SpawnVoidPortal()
    {
      var rand = new Random();
      if (HiddenSpots == null || HiddenSpots.Count == 0)
      {
        throw new InvalidOperationException("HiddenSpots is null or empty.");
      }
      var position = HiddenSpots[rand.Next(HiddenSpots.Count)];
      var actor = SpawnGenericActor("void_portal", position);

      actor.ShouldDespawn = true;
      actor.DespawnTime = 600;

      return actor;
    }

  /// <summary>
  /// Spawns a metal object at a random position.
  /// </summary>
  public WFActor SpawnMetal()
  {
      var rand = new Random();
      List<Vector3> points;

      // Decide whether to use ShorelinePoints or TrashPoints
      if (rand.NextDouble() < 0.15)
      {
          points = ShorelinePoints ?? [];
      }
      else
      {
          points = TrashPoints ?? [];
      }

      // Get a random position from the selected list
      Vector3 position = GetRandomPosition(points, rand);

      // Spawn the actor at the chosen position
      var actor = SpawnGenericActor("metal_spawn", position);
      actor.ShouldDespawn = false; // Metal never despawns

      return actor;
  }

  // Helper method to get a random position from a list
  private static Vector3 GetRandomPosition(List<Vector3> points, Random rand)
  {
      if (points != null && points.Count > 0)
      {
          int index = rand.Next(points.Count);
          return points[index];
      }
      else
      {
          return Vector3.Zero;
      }
  }


    /// <summary>
    /// Finds a server-owned actor by its ID.
    /// </summary>
    private WFActor FindActorByID(long id)
    {
      return ServerOwnedInstances.Find(a => a.InstanceID == id)!;
    }

    /// <summary>
    /// Spawns a generic actor at the specified position.
    /// </summary>
    public WFActor SpawnGenericActor(string type, Vector3? position = null)
    {
      var rand = new Random();
      long instanceId;

      do
      {
        instanceId = rand.NextInt64();
      } while (FindActorByID(instanceId) != null);

      position ??= Vector3.Zero;

      var actor = new WFActor(instanceId, type, position);
      ServerOwnedInstances.Add(actor);

      var spawnPacket = new Dictionary<string, object>
      {
        ["type"] = "instance_actor",
        ["params"] = new Dictionary<string, object>
        {
          ["actor_type"] = type,
          ["at"] = position,
          ["rot"] = new Vector3(0, 0, 0),
          ["zone"] = "main_zone",
          ["zone_owner"] = -1,
          ["actor_id"] = instanceId,
          ["creator_id"] = SteamClient.SteamId.Value
        }
      };

      SendPacketToPlayers(spawnPacket);

      return actor;
    }

    /// <summary>
    /// Removes a server-owned actor and notifies all players.
    /// </summary>
    public void RemoveServerActor(WFActor instance)
    {
      var removePacket = new Dictionary<string, object>
      {
        ["type"] = "actor_action",
        ["actor_id"] = instance.InstanceID,
        ["action"] = "queue_free",
        ["params"] = new Dictionary<int, object>()
      };

      SendPacketToPlayers(removePacket);
      ServerOwnedInstances.Remove(instance);
    }

    /// <summary>
    /// Sends all server-owned actors to a specific player.
    /// </summary>
    private void SendPlayerAllServerActors(SteamId id)
    {
      foreach (var actor in ServerOwnedInstances)
      {
        var spawnPacket = new Dictionary<string, object>
        {
          ["type"] = "instance_actor",
          ["params"] = new Dictionary<string, object>
          {
            ["actor_type"] = actor.Type,
            ["at"] = actor.Position,
            ["rot"] = new Vector3(0, 0, 0),
            ["zone"] = "main_zone",
            ["zone_owner"] = -1,
            ["actor_id"] = actor.InstanceID,
            ["creator_id"] = SteamClient.SteamId.Value
          }
        };

        SendPacketToPlayer(spawnPacket, id);
      }
    }

    /// <summary>
    /// Sends a blacklist packet to a specific player.
    /// </summary>
    public void SendBlacklistPacketToPlayer(string blacklistedSteamID, SteamId receiving)
    {
      var blacklistPacket = new Dictionary<string, object>
      {
        ["type"] = "force_disconnect_player",
        ["user_id"] = blacklistedSteamID
      };

      SendPacketToPlayer(blacklistPacket, receiving);
    }

    /// <summary>
    /// Sends a blacklist packet to all players.
    /// </summary>
    public void SendBlacklistPacketToAll(string blacklistedSteamID)
    {
      var blacklistPacket = new Dictionary<string, object>
      {
        ["type"] = "force_disconnect_player",
        ["user_id"] = blacklistedSteamID
      };

      SendPacketToPlayers(blacklistPacket);
    }

    /// <summary>
    /// Sends a letter to a player and returns the letter ID.
    /// </summary>
    public int SendLetter(SteamId to, SteamId from, string header, string body, string closing, string user)
    {
      // Note: This currently crashes the game.
      var rand = new Random();
      var letterId = rand.Next();

      var letterPacket = new Dictionary<string, object>
      {
        ["type"] = "letter_received",
        ["to"] = to.Value.ToString(),
        ["data"] = new Dictionary<string, object>
        {
          ["to"] = to.Value.ToString(),
          ["from"] = from.Value.ToString(),
          ["header"] = header,
          ["body"] = body,
          ["closing"] = closing,
          ["user"] = user,
          ["letter_id"] = letterId,
          ["items"] = new Dictionary<int, object>()
        }
      };

      SteamNetworking.SendP2PPacket(to, WritePacket(letterPacket), nChannel: 2);

      return letterId;
    }

    /// <summary>
    /// Sends a global message to all players.
    /// </summary>
    public void MessageGlobal(string msg, string color = "ffffff")
    {
      var chatPacket = new Dictionary<string, object>
      {
        ["type"] = "message",
        ["message"] = msg,
        ["color"] = color,
        ["local"] = false,
        ["position"] = new Vector3(0f, 0f, 0f),
        ["zone"] = "main_zone",
        ["zone_owner"] = 1
      };

      foreach (var member in GameLobby.Members)
      {
        if (member.Id == SteamClient.SteamId.Value) continue;
        SteamNetworking.SendP2PPacket(member.Id, WritePacket(chatPacket), nChannel: 2);
      }
    }

    /// <summary>
    /// Sends a message to a specific player.
    /// </summary>
    public void MessagePlayer(string msg, SteamId id, string color = "ffffff")
    {
      var chatPacket = new Dictionary<string, object>
      {
        ["type"] = "message",
        ["message"] = msg,
        ["color"] = color,
        ["local"] = false,
        ["position"] = new Vector3(0f, 0f, 0f),
        ["zone"] = "main_zone",
        ["zone_owner"] = 1
      };

      SteamNetworking.SendP2PPacket(id, WritePacket(chatPacket), nChannel: 2);
    }

    /// <summary>
    /// Sets the zone for a given actor and notifies all players.
    /// </summary>
    public void SetActorZone(WFActor instance, string zoneName, int zoneOwner)
    {
      var actionPacket = new Dictionary<string, object>
      {
        ["type"] = "actor_action",
        ["actor_id"] = instance.InstanceID,
        ["action"] = "_set_zone",
        ["params"] = new Dictionary<int, object>
        {
          [0] = zoneName,
          [1] = zoneOwner
        }
      };

      SendPacketToPlayers(actionPacket);
    }

    /// <summary>
    /// Runs the "_ready" action for an actor and notifies all players.
    /// </summary>
    public void RunActorReady(WFActor instance)
    {
      var actionPacket = new Dictionary<string, object>
      {
        ["type"] = "actor_action",
        ["actor_id"] = instance.InstanceID,
        ["action"] = "_ready",
        ["params"] = new Dictionary<int, object>()
      };

      SendPacketToPlayers(actionPacket);
    }

    /// <summary>
    /// Checks if a player is an admin.
    /// </summary>
    public bool IsPlayerAdmin(SteamId id)
    {
      return Admins.Any(a => long.Parse(a) == (long)id.Value);
    }

    /// <summary>
    /// Updates the player count in the lobby data and console title.
    /// </summary>
    public void UpdatePlayerCount()
    {
      var serverName = ServerName;
      GameLobby.SetData("lobby_name", serverName);
      GameLobby.SetData("name", serverName);

      Console.Title = $"Cove Dedicated Server, {GameLobby.MemberCount - 1} players!";
    }

    /// <summary>
    /// Disconnects all players from the server.
    /// </summary>
    public void DisconnectAllPlayers()
    {
      var closePacket = new Dictionary<string, object> { ["type"] = "server_close" };
      SendPacketToPlayers(closePacket);
    }

    /// <summary>
    /// Creates a response packet for actor requests.
    /// </summary>
    public static Dictionary<string, object> CreateRequestActorResponse()
    {
      return new Dictionary<string, object>
      {
        ["type"] = "actor_request_send",
        ["list"] = new Dictionary<int, object>()
      };
    }

    /// <summary>
    /// Prints a log message from a plugin.
    /// </summary>
    public void PrintPluginLog(string message, CovePlugin caller)
    {
      var pluginInfo = LoadedPlugins.Find(i => i.plugin == caller);
      if (pluginInfo != null)
      {
        Console.WriteLine($"[{pluginInfo.pluginName}] {message}");
      }
      else
      {
        Console.WriteLine($"[Unknown Plugin] {message}");
      }
    }

    // Assume the following helper methods exist elsewhere in your codebase:
    // - SendPacketToPlayers
    // - SendPacketToPlayer
    // - WritePacket
    // - ServerOwnedInstances (List<WFActor>)
    // - AllPlayers (List<WFPlayer>)
    // - Admins (List<string>)
    // - GameLobby (Lobby object)
    // - FishPoints, HiddenSpots, ShorelinePoints, TrashPoints (List<Vector3>)
    // - WFActor, WFPlayer, RainCloud (custom classes)
    // - LoadedPlugins (List<PluginInstance>)
  }
}
