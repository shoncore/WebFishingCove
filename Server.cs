using Steamworks;
using Steamworks.Data;
using WFSermver;

namespace WFSermver
{
    public partial class Server
    {
        string WebFishingGameVersion = "1.08";
        int MaxPlayers = 50;
        string ServerName = "A Cove Dedicated Server";
        string LobbyCode = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
        bool codeOnly = true;

        float rainChance = 0f;

        List<string> Admins = new();

        public List<WebFisher> AllPlayers = new();

        List<WFInstance> serverOwnedInstances = new();
        public Steamworks.Data.Lobby gameLobby = new Steamworks.Data.Lobby();

        Thread cbThread;
        Thread networkThread;

        List<Vector3> fish_points;
        List<Vector3> trash_points;
        List<Vector3> shoreline_points;

        public void Init()
        {

            cbThread = new(runSteamworksUpdate);
            networkThread = new(RunNetwork);

            Console.WriteLine("Loading world!");
            string worldFile = $"{AppDomain.CurrentDomain.BaseDirectory}worlds/main_zone.tscn";
            if (!File.Exists(worldFile))
            {

                Console.WriteLine("-- ERROR --");
                Console.WriteLine("main_zone.tscn is missing!");
                Console.WriteLine("please put a world file in the /worlds folder so the server may load it!");
                Console.WriteLine("-- ERROR --");
                Console.WriteLine("Press any key to exit");

                Console.ReadKey();

                return;
            }

            // get all the spawn points for fish!
            fish_points = ReadWorldFile.readPoints("fish_spawn", File.ReadAllText(worldFile));
            trash_points = ReadWorldFile.readPoints("trash_point", File.ReadAllText(worldFile));
            shoreline_points = ReadWorldFile.readPoints("shoreline_point", File.ReadAllText(worldFile));

            Console.WriteLine("World Loaded!");

            Console.WriteLine("Reading server.cfg");

            Dictionary<string, string> config = ConfigReader.ReadConfig("server.cfg");
            foreach (string key in config.Keys)
            {
                switch (key)
                {
                    case "serverName":
                        ServerName = config[key];
                        break;

                    case "maxPlayers":
                        MaxPlayers = int.Parse(config[key]);
                        break;

                    case "code":
                        LobbyCode = config[key].ToUpper();
                        break;

                    case "codeOnly":
                        {
                            if (config[key].ToLower() == "true")
                            {
                                codeOnly = true;
                            }
                            else if (config[key].ToLower() == "false")
                            {
                                codeOnly = false;
                            }
                            else
                            {
                                Console.WriteLine($"\"{config[key]}\" is not true or false!");
                            }
                        }
                        break;

                    case "gameVersion":
                        WebFishingGameVersion = config[key];
                        break;

                    default:
                        Console.WriteLine($"\"{key}\" is not a supported config option!");
                        break;
                }
            }

            Console.WriteLine("Server setup based on config!");

            Console.WriteLine("Reading admins!");
            readAdmins();
            Console.WriteLine("Setup finished, starting server!");

            try
            {
                SteamClient.Init(3146520, false);
            }
            catch (SystemException e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            cbThread.IsBackground = true;
            cbThread.Start();

            networkThread.IsBackground = true;
            networkThread.Start();

            // start network updates
            Repeat clientUpdateRate = new Repeat(actorUpdate, 1000 / 12); // 8 updates per second should be more than enough!
            clientUpdateRate.Start();

            // host spawning objects
            Repeat hostSpawnTimer = new Repeat(hostSpawn, 10000);
            hostSpawnTimer.Start();


            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            void OnLobbyCreated(Result result, Steamworks.Data.Lobby Lobby)
            {
                Lobby.SetJoinable(true); // make the server joinable to players!
                Lobby.SetData("ref", "webfishing_gamelobby");
                Lobby.SetData("version", WebFishingGameVersion);
                Lobby.SetData("code", LobbyCode);
                Lobby.SetData("type", codeOnly ? "code_only" : "public");
                Lobby.SetData("public", "true");
                Lobby.SetData("banned_players", "");
                Lobby.SetData("age_limit", "false");
                Lobby.SetData("cap", MaxPlayers.ToString());

                SteamNetworking.AllowP2PPacketRelay(true);

                Lobby.SetData("server_browser_value", "0"); // i have no idea!

                Console.WriteLine("Lobby Created!");
                Console.WriteLine($"Lobby Code: {Lobby.GetData("code")}");

                gameLobby = Lobby;

                // set the player count in the title
                updatePlayercount();
            }

            SteamMatchmaking.OnLobbyMemberJoined += void (Lobby Lobby, Friend userJoining) =>
            {
                Console.WriteLine($"{userJoining.Name} [{userJoining.Id}] has joined the game!");
                updatePlayercount();

                WebFisher newPlayer = new WebFisher(userJoining.Id, userJoining.Name);
                AllPlayers.Add(newPlayer);

                Console.WriteLine($"{userJoining.Name} has been assigned the fisherID: {newPlayer.FisherID}");
            };

            SteamMatchmaking.OnLobbyMemberLeave += void (Lobby Lobby, Friend userLeaving) =>
            {
                Console.WriteLine($"{userLeaving.Name} [{userLeaving.Id}] has left the game!");
                updatePlayercount();

                foreach (var player in AllPlayers)
                {
                    if (player.SteamId == userLeaving.Id)
                    {
                        AllPlayers.Remove(player);
                        Console.WriteLine($"{userLeaving.Name} has been removed!");
                    }
                }
            };

            SteamNetworking.OnP2PSessionRequest += void (SteamId id) => {
                foreach (Friend user in gameLobby.Members)
                {
                    if (user.Id == id.Value)
                    {
                        Console.WriteLine($"{user.Name} has connected via P2P");
                        SteamNetworking.AcceptP2PSessionWithUser(id);
                        return;
                    }
                }

                Console.WriteLine($"Got P2P request from {id.Value}, but they are not in the lobby!");
            };

            // create the server
            SteamMatchmaking.CreateLobbyAsync(maxMembers: MaxPlayers);
        }

        void runSteamworksUpdate()
        {
            while (true)
            {
                SteamClient.RunCallbacks();
            }
        }

        void RunNetwork()
        {
            while (true)
            {
                try
                {
                    for (int i = 0; i < 6; i++)
                    {
                        // we are going to check if there are any incoming net packets!
                        if (SteamNetworking.IsP2PPacketAvailable(channel: i))
                        {
                            Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel: i);
                            if (packet != null)
                            {
                                OnNetworkPacket(packet.Value);
                            }
                        }
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine("-- Error responding to packet! --");
                    Console.WriteLine(e.ToString());
                }
            }
        }

        // how many ticks before the server send an update just incase
        int idelUpdateCount = 30;
        Dictionary<int, Vector3> pastTransforms = new Dictionary<int, Vector3>();
        int updateI = 0;
        int actorUpdate()
        {
            updateI++;

            foreach (WFInstance actor in serverOwnedInstances)
            {
                if (actor is RainCloud)
                {
                    actor.onUpdate();
                }

                if (!pastTransforms.ContainsKey(actor.InstanceID))
                {
                    pastTransforms[actor.InstanceID] = Vector3.zero;
                }

                if (actor.pos != pastTransforms[actor.InstanceID] || (updateI == idelUpdateCount))
                {

                    //Console.WriteLine("Updating Actor");

                    Dictionary<string, object> packet = new Dictionary<string, object>();
                    packet["type"] = "actor_update";
                    packet["actor_id"] = actor.InstanceID;
                    packet["pos"] = actor.pos;
                    packet["rot"] = actor.rot;

                    pastTransforms[actor.InstanceID] = actor.pos; // crude

                    sendPacketToPlayers(packet);
                }
            }

            if (updateI >= idelUpdateCount)
            {
                updateI = 0;
            }

            return 0;
        }

        // port of the _host_spawn_object(): in the world.gd script from the game!
        int hostSpawn()
        {

            // remove old instances!
            foreach (WFInstance inst in serverOwnedInstances)
            {
                float instanceAge = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - inst.SpawnTime.ToUnixTimeSeconds();
                if (inst.Type == "fish_spawn_alien" && instanceAge > 120)
                {
                    removeServerActor(inst);
                }
                if (inst.Type == "fish" && instanceAge > 80)
                {
                    removeServerActor(inst);
                }
                if (inst.Type == "raincloud" && instanceAge > 550)
                {
                    removeServerActor(inst);
                }
            }

            // dont spawn too many because it WILL LAG players!
            if (serverOwnedInstances.Count > 15)
            {
                return 0;
            }

            Random ran = new Random();
            string[] beginningTypes = new string[2];
            beginningTypes[0] = "fish";
            beginningTypes[1] = "none";
            string type = beginningTypes[ran.Next() % 2];

            if (ran.NextSingle() < 0.01 && ran.NextSingle() < 0.4)
            {
                type = "meteor";
            }

            if (ran.NextSingle() < rainChance && ran.NextSingle() < .12f)
            {
                type = "rain";
                rainChance = 0;
            }
            else
            {
                if (ran.NextSingle() < .75f)
                    rainChance += .001f;
            }

            switch (type)
            {

                case "none":
                    break;

                case "fish":
                    spawnFish();
                    break;

                case "meteor":
                    spawnFish("fish_spawn_alien");
                    break;

                case "rain":
                    spawnRainCloud();
                    break;

            }

            return 0;
        }
    }
}


/*
void printArray(Dictionary<int, object> obj, string sub = "")
{
    foreach (var kvp in obj)
    {
        if (kvp.Value is Dictionary<string, object>)
        {
            printStringDict((Dictionary<string, object>)kvp.Value, sub + "." + kvp.Key);
        }
        else if (kvp.Value is Dictionary<int, object>)
        {
            printArray((Dictionary<int, object>)kvp.Value, sub + "." + kvp.Key);
        }
        {
            Console.WriteLine($"{sub} {kvp.Key}: {kvp.Value}");
        }
    }
}

void printStringDict(Dictionary<string, object> obj, string sub = "")
{
    foreach (var kvp in obj)
    {
        if (kvp.Value is Dictionary<string, object>)
        {
            printStringDict((Dictionary<string, object>) kvp.Value, sub + "." + kvp.Key);
        } else if(kvp.Value is Dictionary<int, object>)
        {
            printArray((Dictionary<int, object>)kvp.Value, sub + "." + kvp.Key);
        }
        {
            Console.WriteLine($"{sub} {kvp.Key}: {kvp.Value}");
        }
    }
}
*/

/*
// request player pings
var messageTimer = new System.Timers.Timer(5000); // An update rate of 5 seconds
messageTimer.Elapsed += MessageTimer_Elapsed;
void MessageTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
{
    Dictionary<string, object> pingPacket = new();
    pingPacket["type"] = "request_ping";
    pingPacket["sender"] = SteamClient.SteamId.Value.ToString();

    sendPacketToPlayers(pingPacket);
}
messageTimer.AutoReset = true;
messageTimer.Enabled = true; // start the timer!
messageTimer.Start();
*/
