using Steamworks;
using Cove.Server.Plugins;
using Cove.GodotFormat;
using Cove.Server.Actor;
using Cove.Server.Utils;

namespace Cove.Server
{
    partial class CoveServer
    {
        public void readAdmins()
        {
            Dictionary<string, string> config = ConfigReader.ReadConfig("admins.cfg");

            Admins.Clear();

            foreach (string key in config.Keys)
            {
                if (config[key].ToLower() == "true")
                {
                    Console.WriteLine($"Added {key} as admin!");
                    Admins.Add(key);
                    WFPlayer player = AllPlayers.Find(p => p.SteamId.Value.ToString() == key);
                    if (player != null)
                    {
                        messagePlayer("You are an admin on this server!", player.SteamId);
                    }
                }
            }
        }

        public void spawnRainCloud()
        {
            Random rand = new Random();
            Dictionary<string, object> rainSpawnPacket = new Dictionary<string, object>();

            rainSpawnPacket["type"] = "instance_actor";

            int IId = new Random().Next();

            Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
            rainSpawnPacket["params"] = instanceSpacePrams;

            Vector3 pos = new Vector3(rand.Next(-100, 150), 42f, rand.Next(-150, 100));

            instanceSpacePrams["actor_type"] = "raincloud";
            instanceSpacePrams["at"] = pos;
            instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
            instanceSpacePrams["zone"] = "main_zone";
            instanceSpacePrams["zone_owner"] = -1;
            instanceSpacePrams["actor_id"] = IId;
            instanceSpacePrams["creator_id"] = (long)SteamClient.SteamId.Value;

            sendPacketToPlayers(rainSpawnPacket); // spawn the rain!
            serverOwnedInstances.Add(new RainCloud(IId, pos));
        }

        public void spawnFish(string fishType = "fish_spawn")
        {
            Vector3 pos = fish_points[(new Random()).Next(fish_points.Count)];
            spawnGenericActor(fishType, pos);
        }

        public void spawnVoidPortal()
        {
            Vector3 pos = hidden_spot[(new Random()).Next(hidden_spot.Count)];
            spawnGenericActor("void_portal", pos);
        }

        public void spawnMetal()
        {
            Vector3 pos = trash_points[(new Random()).Next(trash_points.Count)];
            if (new Random().NextSingle() < .15f)
            {
                pos = shoreline_points[(new Random()).Next(shoreline_points.Count)];
            }
            spawnGenericActor("metal_spawn", pos);
        }

        public WFActor spawnGenericActor(string type, Vector3 pos = null)
        {
            Dictionary<string, object> spawnPacket = new Dictionary<string, object>();

            spawnPacket["type"] = "instance_actor";

            int IId = new Random().Next();

            Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
            spawnPacket["params"] = instanceSpacePrams;

            if (pos == null)
                pos = Vector3.zero;

            WFActor actor = new WFActor(IId, type, pos);
            serverOwnedInstances.Add(actor);

            instanceSpacePrams["actor_type"] = type;
            instanceSpacePrams["at"] = pos;
            instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
            instanceSpacePrams["zone"] = "main_zone";
            instanceSpacePrams["zone_owner"] = -1;
            instanceSpacePrams["actor_id"] = IId;
            instanceSpacePrams["creator_id"] = (long)SteamClient.SteamId.Value;

            sendPacketToPlayers(spawnPacket);

            return actor;
        }

        public void removeServerActor(WFActor instance)
        {
            Dictionary<string, object> removePacket = new();
            removePacket["type"] = "actor_action";
            removePacket["actor_id"] = instance.InstanceID;
            removePacket["action"] = "queue_free";

            Dictionary<int, object> prams = new Dictionary<int, object>();
            removePacket["params"] = prams;

            sendPacketToPlayers(removePacket); // remove

            serverOwnedInstances.Remove(instance);
        }

        private void sendPlayerAllServerActors(SteamId id)
        {
            foreach (WFActor actor in serverOwnedInstances)
            {
                Dictionary<string, object> spawnPacket = new Dictionary<string, object>();
                spawnPacket["type"] = "instance_actor";

                Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
                spawnPacket["params"] = instanceSpacePrams;

                instanceSpacePrams["actor_type"] = actor.Type;
                instanceSpacePrams["at"] = actor.pos;
                instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
                instanceSpacePrams["zone"] = "main_zone";
                instanceSpacePrams["zone_owner"] = -1;
                instanceSpacePrams["actor_id"] = actor.InstanceID;
                instanceSpacePrams["creator_id"] = (long)SteamClient.SteamId.Value;

                sendPacketToPlayer(spawnPacket, id);
            }
        }

        // returns the letter id!
        int SendLetter(SteamId to, SteamId from, string header, string body, string closing, string user)
        {

            // dosent work atm
            // return -1;

            // Crashes the game lmao
            Dictionary<string, object> letterPacket = new();
            letterPacket["type"] = "letter_received";
            letterPacket["to"] = (string)to.Value.ToString();
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["to"] = (string)to.Value.ToString();
            data["from"] = (string)from.Value.ToString();
            data["header"] = header;
            data["body"] = body;
            data["closing"] = closing;
            data["user"] = user;
            data["letter_id"] = new Random().Next();
            data["items"] = new Dictionary<int, object>();
            letterPacket["data"] = data;

            SteamNetworking.SendP2PPacket(to, writePacket(letterPacket), nChannel: 2);

            return (int)data["letter_id"];
        }

        public void messageGlobal(string msg, string color = "ffffff")
        {
            Dictionary<string, object> chatPacket = new();
            chatPacket["type"] = "message";
            chatPacket["message"] = msg;
            chatPacket["color"] = color;
            chatPacket["local"] = false;
            chatPacket["position"] = new Vector3(0f, 0f, 0f);
            chatPacket["zone"] = "main_zone";
            chatPacket["zone_owner"] = 1;

            // get all players in the lobby
            foreach (Friend member in gameLobby.Members)
            {
                if (member.Id == SteamClient.SteamId.Value) continue;
                SteamNetworking.SendP2PPacket(member.Id, writePacket(chatPacket), nChannel: 2);
            }
        }

        public void messagePlayer(string msg, SteamId id, string color = "ffffff")
        {
            Dictionary<string, object> chatPacket = new();
            chatPacket["type"] = "message";
            chatPacket["message"] = msg;
            chatPacket["color"] = color;
            chatPacket["local"] = (bool)false;
            chatPacket["position"] = new Vector3(0f, 0f, 0f);
            chatPacket["zone"] = "main_zone";
            chatPacket["zone_owner"] = 1;

            SteamNetworking.SendP2PPacket(id, writePacket(chatPacket), nChannel: 2);
        }

        public void setActorZone(WFActor instance, string zoneName, int zoneOwner)
        {
            Dictionary<string, object> removePacket = new();
            removePacket["type"] = "actor_action";
            removePacket["actor_id"] = instance.InstanceID;
            removePacket["action"] = "_set_zone";

            Dictionary<int, object> prams = new Dictionary<int, object>();
            removePacket["params"] = prams;

            prams[0] = zoneName;
            prams[1] = zoneOwner;

            sendPacketToPlayers(removePacket); // remove
        }

        public bool isPlayerAdmin(SteamId id)
        {
            string adminSteamID = Admins.Find(a => long.Parse(a) == long.Parse(id.ToString()));
            return adminSteamID is string;
        }

        void updatePlayercount()
        {
            string serverName = $"{ServerName}";
            gameLobby.SetData("lobby_name", serverName); // not sure what this dose rn
            gameLobby.SetData("name", serverName);

            Console.Title = $"Cove Dedicated Server, {gameLobby.MemberCount - 1} players!";
        }

        public void disconnectAllPlayers()
        {
            Dictionary<string, object> closePacket = new();
            closePacket["type"] = "server_close";

            sendPacketToPlayers(closePacket);
        }

        public void kickPlayer(SteamId id)
        {
            Dictionary<string, object> kickPacket = new();
            kickPacket["type"] = "kick";

            sendPacketToPlayer(kickPacket, id);
        }

        public Dictionary<string, object> createRequestActorResponce()
        {
            Dictionary<string, object> createPacket = new();

            createPacket["type"] = "actor_request_send";

            Dictionary<int, object> actorArray = new();
            createPacket["list"] = actorArray;

            return createPacket;
        }

        public void printPluginLog(string message, CovePlugin caller)
        {

            PluginInstance pluginInfo = loadedPlugins.Find(i => i.plugin == caller);
            Console.WriteLine($"[{pluginInfo.pluginName}] {message}");
        }
    }
}
