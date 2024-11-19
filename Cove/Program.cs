var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});
ILogger<CoveServer> logger = loggerFactory.CreateLogger<CoveServer>();

// Instantiate the CoveServer with the logger and loggerFactory
var webfishingServer = new CoveServer(logger, loggerFactory);

// Start the server
await webfishingServer.InitAsync();

// Handle console cancel key press event
Console.CancelKeyPress += Console_CancelKeyPress;

void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    logger.LogInformation("Application is closing...");

    webfishingServer.DisconnectAllPlayers();
    webfishingServer.GameLobby.Leave(); // Close the lobby
    SteamClient.Shutdown();
    Environment.Exit(0);
}

// Command loop
while (true)
{
    string input = Console.ReadLine()!;
    string[] inputArgs = input.Split(' ', 2);
    string command = inputArgs[0];

    switch (command.ToLower())
    {
        case "exit":
            logger.LogInformation("Application is closing...");
            webfishingServer.DisconnectAllPlayers();
            webfishingServer.GameLobby.Leave();
            SteamClient.Shutdown();
            Environment.Exit(0);
            break;

        case "say":
            if (args.Length > 1)
            {
                string message = args[1];
                webfishingServer.MessageGlobal("Server: {Message}", message);
                logger.LogInformation("Server: {Message}", message);
            }
            else
            {
                logger.LogInformation("Usage: say <message>");
            }
            break;

        case "ban":
            if (args.Length > 1)
            {
                string identifier = args[1];
                WFPlayer? player = null;

                if (ulong.TryParse(identifier, out ulong steamIdValue))
                {
                    SteamId steamId = new() { Value = steamIdValue };
                    player = webfishingServer.AllPlayers.Find(p => p.SteamId == steamId);
                }
                else
                {
                    player = webfishingServer.AllPlayers.Find(p => p.FisherName.Equals(identifier, StringComparison.OrdinalIgnoreCase));
                }

                if (player != null)
                {
                    if (webfishingServer.IsPlayerBanned(player.SteamId))
                    {
                        logger.LogInformation("Player {Username}, [{SteamId}] is already banned!", player.FisherName, player.SteamId);
                    }
                    else
                    {
                        webfishingServer.BanPlayer(player.SteamId, true);
                        logger.LogInformation("Banned player {Username}, [{SteamId}]", player.FisherName, player.SteamId);
                    }
                }
                else
                {
                    logger.LogWarning("Player not found!");
                }
            }
            else
            {
                logger.LogInformation("Usage: ban <player name or Steam ID>");
            }
            break;

        case "kick":
            if (args.Length > 1)
            {
                string identifier = args[1];
                WFPlayer? player = null;

                // Attempt to parse the identifier as a Steam ID (ulong)
                if (ulong.TryParse(identifier, out ulong steamIdValue))
                {
                    // Identifier is a Steam ID
                    SteamId steamId = new() { Value = steamIdValue };
                    player = webfishingServer.AllPlayers.Find(p => p.SteamId == steamId);
                }
                else
                {
                    // Identifier is a player name
                    player = webfishingServer.AllPlayers.Find(p => p.FisherName.Equals(identifier, StringComparison.OrdinalIgnoreCase));
                }

                if (player != null)
                {
                    CoveServer.KickPlayer(player.SteamId);
                    logger.LogInformation("Kicked player {Username}, [{SteamId}]", player.FisherName, player.SteamId);
                }
                else
                {
                    logger.LogWarning("Player with identifier '{Identifier}' not found!", identifier);
                }
            }
            else
            {
                logger.LogInformation("Usage: kick <player name or Steam ID>");
            }
            break;


        case "players":
            logger.LogInformation("Players in server ({Total}):", webfishingServer.AllPlayers.Count);

            foreach (WFPlayer player in webfishingServer.AllPlayers)
            {
                logger.LogInformation("[{Username}]: {SteamId}", player.FisherName, player.SteamId);
            }
            break;

        case "help":
            logger.LogInformation("Commands:");
            logger.LogInformation("exit - Closes the application");
            logger.LogInformation("say <message> - Sends a message to all players");
            logger.LogInformation("ban <player> - Bans a player. <player> can be a player name or Steam ID");
            logger.LogInformation("kick <player> - Kicks a player");
            logger.LogInformation("help - Shows this message");
            logger.LogInformation("players - Lists all players");
            break;

        default:
            logger.LogError("Unknown command! Type 'help' for a list of commands.");
            break;
    }
}
