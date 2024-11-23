namespace Cove.Server
{
    /// <summary>
    /// Represents the Cove Server.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CoveServer"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance.</param>
    /// <param name="loggerFactory">The logger factory instance.</param>
    public partial class CoveServer(ILogger<CoveServer> logger, ILoggerFactory loggerFactory)
    {
        public readonly string WebFishingGameVersion = "1.1";
        public int MaxPlayers { get; private set; } = 24;
        public string ServerName { get; private set; } = "A WebFishing Cove Dedicated Server";
        public string LobbyCode { get; private set; } = GenerateLobbyCode();
        public bool CodeOnly { get; private set; } = true;
        public bool AgeRestricted { get; private set; } = true;
        public bool HideJoinMessage { get; private set; } = false;
        public float RainMultiplier { get; private set; } = 1f;
        public bool ShouldSpawnMeteor { get; private set; } = true;
        public bool ShouldSpawnMetal { get; private set; } = true;
        public bool ShouldSpawnPortal { get; private set; } = true;
        public string DiscordLink { get; private set; } = "https://discord.gg/f5nr6jYPmH";
        public string MessageOfTheDay { get; set; } = "Join our Discord server: https://discord.gg/f5nr6jYPmH";
        public List<string> Admins { get; private set; } = [];
        public Lobby GameLobby { get; private set; }
        public List<WFPlayer> AllPlayers { get; private set; } = [];
        public List<WFActor> ServerOwnedInstances { get; private set; } = [];
        private List<Vector3>? FishPoints { get; set; }
        private List<Vector3>? TrashPoints { get; set; }
        private List<Vector3>? ShorelinePoints { get; set; }
        private List<Vector3>? HiddenSpots { get; set; }
        private Dictionary<string, IHostedService> Services { get; set; } = [];
        private ILogger<CoveServer> Logger { get; set; } = logger;
        private ILoggerFactory LoggerFactory { get; set; } = loggerFactory;

        /// <summary>
        /// Initializes the server asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InitAsync()
        {
            if (!await LoadWorldAsync())
            {
                Logger.LogError("World file missing or invalid. Shutting down.");
                return;
            }

            // Create banned_players.txt if it doesn't already exist
            string banFile = $"{AppDomain.CurrentDomain.BaseDirectory}banned_players.txt";
            if (!File.Exists(banFile))
            {
                FileStream f = File.Create(banFile);
                f.Close();
            }

            // Create admins.cfg if it doesn't already exist
            string adminFile = $"{AppDomain.CurrentDomain.BaseDirectory}admins.cfg";
            if (!File.Exists(adminFile))
            {
                FileStream f = File.Create(adminFile);
                f.Close();
            }

            await SetupConfigurationAsync();

            Logger.LogInformation("Initializing server...");
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamMatchmaking.OnChatMessage += OnChatMessage;
            SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;
            SteamNetworking.OnP2PConnectionFailed += OnP2PConnectionFailed;

            if (!InitializeSteamClient())
            {
                Logger.LogError("Failed to initialize Steam Client. Shutting down.");
                return;
            }

            // Run Steamworks callbacks in a background task
            _ = Task.Run(() => RunSteamworksUpdateAsync());

            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;

            var lobbyResult = await SteamMatchmaking.CreateLobbyAsync(MaxPlayers);
            if (lobbyResult.HasValue)
            {
                Logger.LogInformation("Lobby created successfully.");
                GameLobby = lobbyResult.Value;

                GameLobby.SetJoinable(true);
                GameLobby.SetData("ref", "webfishing_gamelobby");
                GameLobby.SetData("version", WebFishingGameVersion);
                GameLobby.SetData("code", LobbyCode);
                GameLobby.SetData("type", CodeOnly ? "code_only" : "public");
                GameLobby.SetData("public", "true");
                GameLobby.SetData("age_limit", AgeRestricted ? "true" : "false");
                GameLobby.SetData("cap", MaxPlayers.ToString());
                GameLobby.SetData("lobby_name", ServerName);
                GameLobby.SetData("name", ServerName);

                Logger.LogWarning("Lobby code: {LobbyCode}", LobbyCode);
            }
            else
            {
                Logger.LogError("Failed to create lobby.");
                return;
            }

            Logger.LogInformation("Loading admins...");
            LoadAdmins();

            await SetupPluginsAsync();

            Logger.LogInformation("Starting background tasks...");
            _ = Task.Run(() => ProcessNetworkPacketsAsync());

            await StartHostedServicesAsync();

            Logger.LogInformation("Server is now running.");
        }

        /// <summary>
        /// Generates a random lobby code consisting of 5 alphanumeric characters.
        /// </summary>
        /// <returns>A 5-character string lobby code.</returns>
        private static string GenerateLobbyCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Range(0, 5).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        /// <summary>
        /// Loads the world data asynchronously from the world file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation, with a boolean indicating success.</returns>
        private async Task<bool> LoadWorldAsync()
        {
            var worldFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "worlds", "main_zone.tscn");
            if (!File.Exists(worldFilePath))
            {
                Logger.LogError("World file 'main_zone.tscn' is missing!");
                return false;
            }

            var mapFile = await File.ReadAllTextAsync(worldFilePath);
            FishPoints = WorldFile.ReadPoints("fish_spawn", mapFile);
            TrashPoints = WorldFile.ReadPoints("trash_point", mapFile);
            ShorelinePoints = WorldFile.ReadPoints("shoreline_point", mapFile);
            HiddenSpots = WorldFile.ReadPoints("hidden_spot", mapFile);

            Logger.LogInformation("World file loaded successfully.");
            return true;
        }

        /// <summary>
        /// Sets up the server configuration asynchronously by reading from the configuration file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SetupConfigurationAsync()
        {
            Logger.LogInformation("Reading configuration...");
            ConfigReader reader = new(LoggerFactory.CreateLogger<ConfigReader>());
            var config = reader.ReadConfig("server.cfg");

            foreach (var (key, value) in config)
            {
                try
                {
                    switch (key)
                    {
                        case "serverName":
                            ServerName = value;
                            break;
                        case "maxPlayers":
                            MaxPlayers = int.Parse(value);
                            break;
                        case "code":
                            LobbyCode = value.ToUpper();
                            break;
                        case "rainSpawnMultiplier":
                            RainMultiplier = float.Parse(value);
                            break;
                        case "codeOnly":
                            CodeOnly = GetBoolFromString(value);
                            break;
                        case "ageRestricted":
                            AgeRestricted = GetBoolFromString(value);
                            break;
                        case "hideJoinMessage":
                            HideJoinMessage = GetBoolFromString(value);
                            break;
                        case "pluginsEnabled":
                            PluginsEnabled = GetBoolFromString(value);
                            break;
                        case "spawnMeteor":
                            ShouldSpawnMeteor = GetBoolFromString(value);
                            break;
                        case "spawnMetal":
                            ShouldSpawnMetal = GetBoolFromString(value);
                            break;
                        case "spawnPortal":
                            ShouldSpawnPortal = GetBoolFromString(value);
                            break;
                        case "discordLink":
                            DiscordLink = value;
                            break;
                        default:
                            if (!UnhandledKeyLogger.KeyExists(value))
                            {
                                Logger.LogWarning("Unhandled config key: {Key}", key);
                                UnhandledKeyLogger.AddKey(key);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Error parsing config '{Key}': {ErrorMessage}", key, ex.Message);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Loads the list of admins from the 'admins.cfg' file.
        /// </summary>
        private void LoadAdmins()
        {
            var adminsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "admins.cfg");
            if (File.Exists(adminsFilePath))
            {
                Admins = [.. File.ReadAllLines(adminsFilePath)];
                Logger.LogInformation("{TotalAdmins} admins loaded.", Admins.Count);
            }
            else
            {
                Logger.LogWarning("Admins file not found. No admins loaded.");
            }
        }

        /// <summary>
        /// Sets up the plugins asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SetupPluginsAsync()
        {
            var pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (!Directory.Exists(pluginsPath))
            {
                Directory.CreateDirectory(pluginsPath);
                Logger.LogInformation("Created plugins directory.");
            }

            await Task.Run(() =>
            {
                LoadAllPlugins();
            });
        }

        /// <summary>
        /// Initializes the Steam Client.
        /// </summary>
        /// <returns>True if initialization is successful; otherwise, false.</returns>
        private bool InitializeSteamClient()
        {
            try
            {
                SteamClient.Init(SteamConfig.AppId, false);
                Logger.LogInformation("Steam Client initialized successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize Steam Client!");
                return false;
            }
        }

        /// <summary>
        /// Starts the hosted services asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task StartHostedServicesAsync()
        {
            Logger.LogInformation("Starting hosted services...");
            var actorUpdateLogger = LoggerFactory.CreateLogger<ActorUpdateService>();
            var hostSpawnLogger = LoggerFactory.CreateLogger<HostSpawnService>();
            var hostSpawnMetalLogger = LoggerFactory.CreateLogger<HostSpawnMetalService>();

            // Initialize services with appropriate loggers
            Services["actor_update"] = new ActorUpdateService(actorUpdateLogger, this);
            Services["host_spawn"] = new HostSpawnService(hostSpawnLogger, this);
            Services["host_spawn_metal"] = new HostSpawnMetalService(hostSpawnMetalLogger, this);

            foreach (var service in Services.Values)
            {
                await service.StartAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// Continuously runs Steamworks callbacks in a background task.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task RunSteamworksUpdateAsync()
        {
            while (true)
            {
                SteamClient.RunCallbacks();
                await Task.Delay(16);
            }
        }

        /// <summary>
        /// Processes network packets asynchronously in a background task.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessNetworkPacketsAsync()
        {
            while (true)
            {
                try
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (SteamNetworking.IsP2PPacketAvailable(channel: i))
                        {
                            var packet = SteamNetworking.ReadP2PPacket(channel: i);
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

                await Task.Delay(10); // Delay to prevent tight loop
            }
        }


        /// <summary>
        /// Converts a string to a boolean value.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <returns>True if the string is "true" (case-insensitive); otherwise, false.</returns>
        private static bool GetBoolFromString(string value) =>
            value.Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets an enumerable of server-owned actor instances.
        /// </summary>
        /// <returns>An enumerable of <see cref="WFActor"/>.</returns>
        public IEnumerable<WFActor> GetServerOwnedInstances()
        {
            return ServerOwnedInstances;
        }

        private void OnLobbyCreated(Result result, Lobby gameLobby)
        {
            gameLobby.SetJoinable(true);
            gameLobby.SetData("ref", "webfishing_gamelobby");
            gameLobby.SetData("version", WebFishingGameVersion);
            gameLobby.SetData("code", LobbyCode);
            gameLobby.SetData("type", CodeOnly ? "code_only" : "public");
            gameLobby.SetData("public", "true");
            gameLobby.SetData("age_limit", AgeRestricted ? "true" : "false");
            gameLobby.SetData("cap", MaxPlayers.ToString());
            gameLobby.SetData("lobby_name", ServerName);
            gameLobby.SetData("name", ServerName);

            SteamNetworking.AllowP2PPacketRelay(true);

            // This is WebFishing server-specific. Possibly benign value to determine if server appears on server list.
            gameLobby.SetData("server_browser_value", "0");
            GameLobby = gameLobby;
            Logger.LogWarning("YOUR LOBBY CODE: {LobbyCode}", LobbyCode);
            UpdatePlayerCount();
        }

        private void OnLobbyMemberJoined(Lobby gameLobby, Friend friend)
        {
            var player = AllPlayers.Find(p => p.SteamId.Value == friend.Id.Value);

            if (player == null) {
              player = new(friend.Id.Value, friend.Name);
              AllPlayers.Add(player);
            }

            PlayerLogger.LogPlayerJoined(
              Logger,
              AllPlayers.Count,
              player,
              friend
            );

            UpdatePlayerCount();

            foreach (PluginInstance plugin in LoadedPlugins)
            {
                plugin.plugin.OnPlayerJoin(player);
            }
        }

        private void OnLobbyMemberLeave (Lobby gameLobby, Friend friend)
        {
            var player = AllPlayers.Find(p => p.SteamId.Value == friend.Id.Value);

            if (player == null)
            {
                Logger.LogError("Player {Name} with SteamId: {SteamId} not found in AllPlayers list.", friend.Name, friend.Id.Value);
                return;
            }

            AllPlayers.Remove(player);
            PlayerLogger.LogPlayerLeft(
              Logger,
              AllPlayers.Count,
              player,
              friend
            );
            UpdatePlayerCount();
        }

        private void OnChatMessage(Lobby gameLobby, Friend friend, string message)
        {
          Logger.LogWarning("USER: {Message}", message);
          var player = AllPlayers.Find(p => p.SteamId.Value == friend.Id.Value);
          if (player == null)
          {
              Logger.LogError("Player {Name} with SteamId: {SteamId} not found in AllPlayers list.", friend.Name, friend.Id.Value);
              return;
          }

          Logger.LogInformation("{FisherName} ({SteamId}): {Message}", player.FisherName, friend.Id.Value, message);

          foreach (PluginInstance plugin in LoadedPlugins)
          {
              plugin.plugin.OnChatMessage(player, message);
          }
        }

        private void OnP2PSessionRequest(SteamId steamId)
        {
            if (GameLobby.Members.Any(f => f.Id.Value == steamId.Value))
            {
                Logger.LogInformation("Accepting P2P session request from {SteamId}.", steamId);
                SteamNetworking.AcceptP2PSessionWithUser(steamId);
            }
        }

        private void OnP2PConnectionFailed(SteamId steamId, P2PSessionError error)
        {
            Logger.LogWarning("P2P connection failed with {SteamId}: {Error}", steamId, error);
        }
    }
}
