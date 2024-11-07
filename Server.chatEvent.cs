using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFSermver
{
    partial class Server
    {
        void OnPlayerChat(string message, SteamId id)
        {
            WebFisher sender = AllPlayers.Find(p => p.SteamId == id);
            Console.WriteLine($"{sender.FisherName}: {message}");

            char[] msg = message.ToCharArray();
            if (msg[0] == "!".ToCharArray()[0]) // its a command!
            {
                string command = message.Split(" ")[0].ToLower();
                switch (command)
                {
                    case "!users":
                        if (!isPlayerAdmin(id)) return;
                        string messageBody = "";
                        foreach (var player in AllPlayers)
                        {
                            messageBody += $"{player.FisherName} [{player.SteamId}]: {player.FisherID}\n";
                        }

                        SendLetter(id, SteamClient.SteamId, "Players in the server", messageBody, "Always here - ", "Cove");

                        break;

                    case "!spawnrain":
                        if (!isPlayerAdmin(id)) return;
                        messagePlayer("spawning!", id);
                        spawnRainCloud();
                        break;

                    case "!spawnfish":
                        if (!isPlayerAdmin(id)) return;
                        spawnFish();
                        break;

                    case "!spawnmeteor":
                        if (!isPlayerAdmin(id)) return;
                        spawnFish("fish_spawn_alien");
                        break;

                    case "!kick":
                        if (!isPlayerAdmin(id)) return;
                        var kickUser = message.Split(" ")[1].ToUpper();
                        WebFisher kickedplayer = AllPlayers.Find(p => p.FisherID == kickUser);
                        if (kickedplayer == null)
                        {
                            messagePlayer("That's not a player!", id);
                        }
                        else
                        {
                            Dictionary<string, object> packet = new Dictionary<string, object>();
                            packet["type"] = "kick";

                            SteamNetworking.SendP2PPacket(kickedplayer.SteamId, writePacket(packet), nChannel: 2);

                            messagePlayer($"Kicked {kickedplayer.FisherName}", id);
                            messageGlobal($"{kickedplayer.FisherName} was kicked from the lobby!");
                        }
                        break;

                    case "!setjoinable":
                        {
                            if (!isPlayerAdmin(id)) return;
                            string arg = message.Split(" ")[1].ToLower();
                            if (arg == "true")
                            {
                                gameLobby.SetJoinable(true);
                                messagePlayer($"Opened lobby!", id);
                                if (!codeOnly)
                                {
                                    gameLobby.SetData("type", "public");
                                    messagePlayer($"Unhid server from server list", id);
                                }
                            }
                            else if (arg == "false")
                            {
                                gameLobby.SetJoinable(false);
                                messagePlayer($"Closed lobby!", id);
                                if (!codeOnly)
                                {
                                    gameLobby.SetData("type", "code_only");
                                    messagePlayer($"Hid server from server list", id);
                                }
                            }
                            else
                            {
                                messagePlayer($"\"{arg}\" is not true or false!", id);
                            }
                        }
                        break;

                    case "!updateadmins":
                        {
                            if (!isPlayerAdmin(id)) return;
                            readAdmins();
                        }
                        break;

                    case "!talk":
                        {
                            messagePlayer("hello world!", id);
                            Console.WriteLine("Talking to player");
                        }
                        break;

                    case "!wiperain":
                        {
                            if (!isPlayerAdmin(id)) return;
                            WFInstance rain = serverOwnedInstances.Find(i => i.Type == "raincloud");
                            if (rain != null)
                            {
                                removeServerActor(rain);
                            }
                        }
                        break;
                }
            }
        }
    }
}
