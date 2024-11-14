using Cove.Server;
using Cove.Server.Actor;
using Cove.Server.Plugins;

public class ChatCommands : CovePlugin
{
    CoveServer Server { get; set; } // lol
    public ChatCommands(CoveServer server) : base(server)
    {
        Server = server;
    }

    public override void onInit()
    {
        base.onInit();
    }

    public override void onPlayerJoin(WFPlayer player)
    {
        base.onPlayerJoin(player);
    }

    public override void onChatMessage(WFPlayer sender, string message)
    {
        base.onChatMessage(sender, message);

        char[] msg = message.ToCharArray();
        if (msg[0] == "!".ToCharArray()[0]) // its a command!
        {
            string command = message.Split(" ")[0].ToLower();
            switch (command)
            {
                case "!help":
                    {
                        sendPlayerChatMessage(sender, "--- HELP ---");
                        sendPlayerChatMessage(sender, "!help - Shows this message");
                        sendPlayerChatMessage(sender, "!users - Shows all players in the server");
                        sendPlayerChatMessage(sender, "!spawn <actor> - Spawns an actor");
                        sendPlayerChatMessage(sender, "!kick <player> - Kicks a player");
                        sendPlayerChatMessage(sender, "!ban <player> - Bans a player");
                        sendPlayerChatMessage(sender, "!setjoinable <true/false> - Opens or closes the lobby");
                        sendPlayerChatMessage(sender, "!refreshadmins - Refreshes the admins list");
                    }
                    break;

                case "!users":
                    if (!isPlayerAdmin(sender)) return;

                    // Get the command arguments
                    string[] commandParts = message.Split(' ');

                    int pageNumber = 1;
                    int pageSize = 10;

                    // Check if a page number was provided
                    if (commandParts.Length > 1)
                    {
                        if (!int.TryParse(commandParts[1], out pageNumber) || pageNumber < 1)
                        {
                            pageNumber = 1; // Default to page 1 if parsing fails or page number is less than 1
                        }
                    }

                    var allPlayers = getAllPlayers();
                    int totalPlayers = allPlayers.Count();
                    int totalPages = (int)Math.Ceiling((double)totalPlayers / pageSize);

                    // Ensure the page number is within the valid range
                    if (pageNumber > totalPages) pageNumber = totalPages;

                    // Get the players for the current page
                    var playersOnPage = allPlayers.Skip((pageNumber - 1) * pageSize).Take(pageSize);

                    // Build the message to send
                    string messageBody = "";
                    foreach (var player in playersOnPage)
                    {
                        messageBody += $"\n{player.FisherName}: {player.FisherID}";
                    }

                    messageBody += $"\nPage {pageNumber} of {totalPages}";

                    sendPlayerChatMessage(sender, "Players in the server:" + messageBody + "\nAlways here - Cove");
                    break;

                case "!spawn":
                    {
                        if (!isPlayerAdmin(sender)) return;

                        var actorType = message.Split(" ")[1].ToLower();
                        bool spawned = false;
                        switch (actorType)
                        {
                            case "rain":
                                Server.spawnRainCloud();
                                spawned = true;
                                break;

                            case "fish":
                                Server.spawnFish();
                                spawned = true;
                                break;

                            case "meteor":
                                spawned = true;
                                Server.spawnFish("fish_spawn_alien");
                                break;

                            case "portal":
                                Server.spawnVoidPortal();
                                spawned = true;
                                break;

                            case "metal":
                                Server.spawnMetal();
                                spawned = true;
                                break;
                        }
                        if (spawned)
                        {
                            sendPlayerChatMessage(sender, $"Spawned {actorType}");
                        }
                        else
                        {
                            sendPlayerChatMessage(sender, $"\"{actorType}\" is not a spawnable actor!");
                        }
                    }
                    break;

                case "!kick":
                    {
                        if (!isPlayerAdmin(sender)) return;
                        string playerName = message.Substring(command.Length + 1);
                        WFPlayer kickedplayer = getAllPlayers().ToList().Find(p => p.FisherName.Equals(playerName, StringComparison.OrdinalIgnoreCase));
                        if (kickedplayer == null)
                        {
                            sendPlayerChatMessage(sender, "That's not a player!");
                        }
                        else
                        {
                            Dictionary<string, object> packet = new Dictionary<string, object>();
                            packet["type"] = "kick";

                            sendPacketToPlayer(packet, kickedplayer);

                            sendPlayerChatMessage(sender, $"Kicked {kickedplayer.FisherName}");
                            sendGlobalChatMessage($"{kickedplayer.FisherName} was kicked from the lobby!");
                        }
                    }
                    break;
                    
                case "!ban":
                    {
                        if (!isPlayerAdmin(sender)) return;
                        // hacky fix,
                        // Extract player name from the command message
                        string playerName = message.Substring(command.Length + 1);
                        WFPlayer playerToBan = getAllPlayers().ToList().Find(p => p.FisherName.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                        if (playerToBan == null)
                        {
                            sendPlayerChatMessage(sender, "Player not found!");
                        }
                        else
                        {
                            banPlayer(playerToBan);
                            sendPlayerChatMessage(sender, $"Banned {playerToBan.FisherName}");
                            sendGlobalChatMessage($"{playerToBan.FisherName} has been banned from the server.");
                        }
                    }
                    break;
                    
                case "!setjoinable":
                    {
                        if (!isPlayerAdmin(sender)) return;
                        string arg = message.Split(" ")[1].ToLower();
                        if (arg == "true")
                        {
                            Server.gameLobby.SetJoinable(true);
                            sendPlayerChatMessage(sender, $"Opened lobby!");
                            if (!Server.codeOnly)
                            {
                                Server.gameLobby.SetData("type", "public");
                                sendPlayerChatMessage(sender, $"Unhid server from server list");
                            }
                        }
                        else if (arg == "false")
                        {
                            Server.gameLobby.SetJoinable(false);
                            sendPlayerChatMessage(sender, $"Closed lobby!");
                            if (!Server.codeOnly)
                            {
                                Server.gameLobby.SetData("type", "code_only");
                                sendPlayerChatMessage(sender, $"Hid server from server list");
                            }
                        }
                        else
                        {
                            sendPlayerChatMessage(sender, $"\"{arg}\" is not true or false!");
                        }
                    }
                    break;

                case "!refreshadmins":
                    {
                        if (!isPlayerAdmin(sender)) return;
                        Server.readAdmins();
                    }
                    break;
            }
        }

    }

}
