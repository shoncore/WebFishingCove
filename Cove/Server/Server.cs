﻿namespace Cove.Server
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

    /// <summary>
    /// The game version.
    /// </summary>
    public readonly string WebFishingGameVersion = "1.1";

    /// <summary>
    /// Gets the maximum number of players allowed in the server.
    /// </summary>
    public int MaxPlayers { get; private set; } = 24;

    /// <summary>
    /// Gets the name of the server.
    /// </summary>
    public string ServerName { get; private set; } = "A Cove Dedicated Server";

    /// <summary>
    /// Gets the lobby code used for joining the server.
    /// </summary>
    public string LobbyCode { get; private set; } = GenerateLobbyCode();

    /// <summary>
    /// Gets a value indicating whether the server is joinable only by code.
    /// </summary>
    public bool CodeOnly { get; private set; } = true;

    /// <summary>
    /// Gets a value indicating whether the server is age restricted.
    /// </summary>
    public bool AgeRestricted { get; private set; } = false;

    /// <summary>
    /// Gets a value indicating whether the join message is hidden.
    /// </summary>
    public bool HideJoinMessage { get; private set; } = false;

    /// <summary>
    /// Gets the multiplier for rain spawn rate.
    /// </summary>
    public float RainMultiplier { get; private set; } = 1f;

    /// <summary>
    /// Gets a value indicating whether meteors should spawn.
    /// </summary>
    public bool ShouldSpawnMeteor { get; private set; } = true;

    /// <summary>
    /// Gets a value indicating whether metal should spawn.
    /// </summary>
    public bool ShouldSpawnMetal { get; private set; } = true;

    /// <summary>
    /// Gets a value indicating whether portals should spawn.
    /// </summary>
    public bool ShouldSpawnPortal { get; private set; } = true;

    /// <summary>
    /// Gets the list of admin Steam IDs.
    /// </summary>
    public List<string> Admins { get; private set; } = [];

    /// <summary>
    /// Gets the game lobby.
    /// </summary>
    public Lobby GameLobby { get; private set; }

    /// <summary>
    /// Gets the list of all players connected to the server.
    /// </summary>
    public List<WFPlayer> AllPlayers { get; private set; } = [];

    /// <summary>
    /// Gets the list of server-owned actor instances.
    /// </summary>
    public List<WFActor> ServerOwnedInstances { get; private set; } = [];

    /// <summary>
    /// Gets or sets the queue for incoming network packets.
    /// </summary>
    private ConcurrentQueue<P2Packet> PacketQueue { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of fish spawn points.
    /// </summary>
    private List<Vector3>? FishPoints { get; set; }

    /// <summary>
    /// Gets or sets the list of trash spawn points.
    /// </summary>
    private List<Vector3>? TrashPoints { get; set; }

    /// <summary>
    /// Gets or sets the list of shoreline points.
    /// </summary>
    private List<Vector3>? ShorelinePoints { get; set; }

    /// <summary>
    /// Gets or sets the list of hidden spots.
    /// </summary>
    private List<Vector3>? HiddenSpots { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of hosted services.
    /// </summary>
    private Dictionary<string, IHostedService> Services { get; set; } = [];

    /// <summary>
    /// Gets or sets the logger instance for the server.
    /// </summary>
    private ILogger<CoveServer> Logger { get; set; } = logger;

    /// <summary>
    /// Gets or sets the logger factory instance.
    /// </summary>
    private ILoggerFactory LoggerFactory { get; set; } = loggerFactory;

    /// <summary>
    /// Initializes the server asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitAsync()
    {
      Logger.LogInformation("Initializing server...");

      if (!await LoadWorldAsync())
      {
        Logger.LogError("World file missing or invalid. Shutting down.");
        return;
      }

      await SetupConfigurationAsync();

      Logger.LogInformation("Loading admins...");
      LoadAdmins();

      await SetupPluginsAsync();

      if (!InitializeSteamClient())
      {
        Logger.LogError("Failed to initialize Steam Client. Shutting down.");
        return;
      }

      Logger.LogInformation("Starting background tasks...");
      _ = Task.Run(() => RunSteamworksUpdateAsync());
      _ = Task.Run(() => ProcessNetworkPacketsAsync());

      await StartHostedServicesAsync();

      Logger.LogInformation("Server initialization complete. Creating lobby...");
      var lobbyResult = await SteamMatchmaking.CreateLobbyAsync(MaxPlayers);
      if (lobbyResult.HasValue)
      {
        GameLobby = lobbyResult.Value;
      }
      else
      {
        Logger.LogError("Failed to create lobby.");
        return;
      }

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
      var config = ConfigReader.ReadConfig("server.cfg");

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
            case "spawnMeteor":
              ShouldSpawnMeteor = GetBoolFromString(value);
              break;
            case "spawnMetal":
              ShouldSpawnMetal = GetBoolFromString(value);
              break;
            case "spawnPortal":
              ShouldSpawnPortal = GetBoolFromString(value);
              break;
            default:
              Logger.LogWarning("Unsupported configuration key: {Key}", key);
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
        Logger.LogWarning("Admins file not found.");
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
        // Simulate loading plugins
        Logger.LogInformation("Loading plugins...");
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
        SteamClient.Init(3146520, false);
        Logger.LogInformation("Steam Client initialized successfully.");
        return true;
      }
      catch (Exception ex)
      {
        Logger.LogError("Error initializing Steam Client: {ErrorMessage}", ex.Message);
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
        while (PacketQueue.TryDequeue(out var packet))
        {
          OnNetworkPacket(packet);
        }

        await Task.Delay(10);
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
  }
}
