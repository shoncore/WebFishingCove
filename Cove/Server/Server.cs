using Steamworks;
using Steamworks.Data;
using Cove.Server.Plugins;
using Cove.GodotFormat;
using Cove.Server.Actor;
using Cove.Server.Utils;
using Microsoft.Extensions.Hosting;
using Cove.Server.HostedServices;
using Microsoft.Extensions.Logging;

namespace Cove.Server
{
    public partial class CoveServer
    {
        private string WebFishingGameVersion = "1.1";
        public int MaxPlayers = 20;
        public string ServerName = "A Cove Dedicated Server";
        private string LobbyCode = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
        public bool codeOnly = true;
        public bool ageRestricted = false;
        public bool hideJoinMessage = false;

        public float rainMultiplyer = 1f;
        public bool shouldSpawnMeteor = true;
        public bool shouldSpawnMetal = true;
        public bool shouldSpawnPortal = true;

        List<string> Admins = new();

        public List<WFPlayer> AllPlayers = new();

        public List<WFActor> serverOwnedInstances = new();
        public Lobby gameLobby = new Lobby();

        Thread cbThread;
        Thread networkThread;

        List<Vector3> fish_points;
        List<Vector3> trash_points;
        List<Vector3> shoreline_points;
        List<Vector3> hidden_spot;

        Dictionary<string, IHostedService> services = new();

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

            string banFile = $"{AppDomain.CurrentDomain.BaseDirectory}bans.txt";
            if (!File.Exists(banFile))
            {
                FileStream f = File.Create(banFile);
                f.Close(); // close the file
            }

            // get all the spawn points for fish!
            string mapFile = File.ReadAllText(worldFile);
            fish_points = WorldFile.readPoints("fish_spawn", mapFile);
            trash_points = WorldFile.readPoints("trash_point", mapFile);
            shoreline_points = WorldFile.readPoints("shoreline_point", mapFile);
            hidden_spot = WorldFile.readPoints("hidden_spot", mapFile);

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

                    case "rainSpawnMultiplyer":
                        rainMultiplyer = float.Parse(config[key]);
                        break;

                    case "codeOnly":
                        codeOnly = getBoolFromString(config[key]);
                        break;

                    case "gameVersion":
                        WebFishingGameVersion = config[key];
                        break;

                    case "ageRestricted":
                        ageRestricted = getBoolFromString(config[key]);
                        break;

                    case "pluginsEnabled":
                        arePluginsEnabled = getBoolFromString(config[key]);
                        break;

                    case "hideJoinMessage":
                        hideJoinMessage = getBoolFromString(config[key]);
                        break;

                    case "spawnMeteor":
                        shouldSpawnMeteor = getBoolFromString(config[key]);
                        break;

                    case "spawnMetal":
                        shouldSpawnMetal = getBoolFromString(config[key]);
                        break;

                    case "spawnPortal":
                        shouldSpawnPortal = getBoolFromString(config[key]);
                        break;

                    default:
                        Console.WriteLine($"\"{key}\" is not a supported config option!");
                        continue;
                }

                Console.WriteLine($"Set \"{key}\" to \"{config[key]}\"");

            }

            Console.WriteLine("Server setup based on config!");

            Console.WriteLine("Reading admins.cfg");
            readAdmins();
            Console.WriteLine("Setup finished, starting server!");

            if (Directory.Exists($"{AppDomain.CurrentDomain.BaseDirectory}plugins"))
            {
                loadAllPlugins();
            }
            else
            {
                Directory.CreateDirectory($"{AppDomain.CurrentDomain.BaseDirectory}plugins");
                Console.WriteLine("Created plugins folder!");
            }

            try
            {
                SteamClient.Init(3146520, false);
            }
            catch (SystemException e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            // thread for running steamworks callbacks
            cbThread.IsBackground = true;
            cbThread.Start();

            // thread for getting network packets from steam
            // i wish this could be a service, but when i tried it the packets got buffered and it was a mess
            // like 10 minutes of delay within 30 seconds
            networkThread.IsBackground = true;
            networkThread.Start();
            
            bool LogServices = false;
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                if (LogServices)
                    builder.AddConsole();
            });

            // Create a logger for each service that we need to run.
            Logger<ActorUpdateService> actorServiceLogger = new Logger<ActorUpdateService>(loggerFactory);
            Logger<HostSpawnService> hostSpawnServiceLogger = new Logger<HostSpawnService>(loggerFactory);
            Logger<HostSpawnMetalService> hostSpawnMetalServiceLogger = new Logger<HostSpawnMetalService>(loggerFactory);

            // Create the services that we need to run.
            IHostedService actorUpdateService = new ActorUpdateService(actorServiceLogger, this);
            IHostedService hostSpawnService = new HostSpawnService(hostSpawnServiceLogger, this);
            IHostedService hostSpawnMetalService = new HostSpawnMetalService(hostSpawnMetalServiceLogger, this);

            // Start the services.
            actorUpdateService.StartAsync(CancellationToken.None);
            hostSpawnService.StartAsync(CancellationToken.None);
            hostSpawnMetalService.StartAsync(CancellationToken.None);

            services["actor_update"] = actorUpdateService;
            services["host_spawn"] = hostSpawnService;
            services["host_spawn_metal"] = hostSpawnMetalService;

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
                Lobby.SetData("age_limit", ageRestricted ? "true" : "false");
                Lobby.SetData("cap", MaxPlayers.ToString());

                SteamNetworking.AllowP2PPacketRelay(true);

                Lobby.SetData("server_browser_value", "0"); // i have no idea!

                Console.WriteLine("Lobby Created!");
                Console.Write("Lobby Code: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(Lobby.GetData("code"));
                Console.ResetColor();

                gameLobby = Lobby;

                // set the player count in the title
                updatePlayercount();
            }

            SteamMatchmaking.OnLobbyMemberJoined += void (Lobby Lobby, Friend userJoining) =>
            {
                Console.WriteLine($"{userJoining.Name} [{userJoining.Id}] has joined the game!");
                updatePlayercount();

                WFPlayer newPlayer = new WFPlayer(userJoining.Id, userJoining.Name);
                AllPlayers.Add(newPlayer);

                Console.WriteLine($"{userJoining.Name} has been assigned the fisherID: {newPlayer.FisherID}");

                foreach (PluginInstance plugin in loadedPlugins)
                {
                    plugin.plugin.onPlayerJoin(newPlayer);
                }

            };

            SteamMatchmaking.OnLobbyMemberLeave += void (Lobby Lobby, Friend userLeaving) =>
            {
                Console.WriteLine($"{userLeaving.Name} [{userLeaving.Id}] has left the game!");
                updatePlayercount();

                foreach (var player in AllPlayers)
                {
                    if (player.SteamId == userLeaving.Id)
                    {

                        foreach (PluginInstance plugin in loadedPlugins)
                        {
                            plugin.plugin.onPlayerLeave(player);
                        }

                        AllPlayers.Remove(player);
                        Console.WriteLine($"{userLeaving.Name} has been removed!");
                    }
                }
            };

            SteamNetworking.OnP2PSessionRequest += void (SteamId id) =>
            {
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
        private bool getBoolFromString(string str)
        {
            if (str.ToLower() == "true")
            {
                return true;
            }
            else if (str.ToLower() == "false")
            {
                return false;
            }
            else
            {
                return false;
            }
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
