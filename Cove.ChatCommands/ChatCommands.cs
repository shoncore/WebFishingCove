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
        //sendPlayerChatMessage(player, "use !help for a list of commands!");
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
                        sendPlayerChatMessage(sender, "Not much here tbh...");
                    }
                    break;

                case "!users":
                    if (!isPlayerAdmin(sender)) return;
                    string messageBody = "";
                    foreach (var player in getAllPlayers())
                    {
                        messageBody += $"{player.FisherName} [{player.SteamId}]: {player.FisherID}\n";
                    }

                    //SendLetter(id, SteamClient.SteamId, "Players in the server", messageBody, "Always here - ", "Cove");
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
                    if (!isPlayerAdmin(sender)) return;
                    var kickUser = message.Split(" ")[1].ToUpper();
                    WFPlayer kickedplayer = getAllPlayers().ToList().Find(p => p.FisherID == kickUser);
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