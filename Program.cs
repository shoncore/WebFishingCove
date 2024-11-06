using Steamworks;
using Steamworks.Data;
using System.Net.Sockets;
using WFSermver;

var _exitEvent = new ManualResetEvent(false);
var WebFishingGameVersion = "1.08";
int MaxPlayers = 50;
string ServerName = "A Cove Dedicated Server";
string LobbyCode = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
bool codeOnly = true;

float rainChance = 0f;

List<string> Admins = new();

// list of all WebFishers
List<WebFisher> AllPlayers = new();

Console.WriteLine("Loading world!");
string worldFile = $"{AppDomain.CurrentDomain.BaseDirectory}worlds/main_zone.tscn";
if (!File.Exists(worldFile))
{

    Console.WriteLine("-- ERROR --");
    Console.WriteLine("main_zone.tscn is missing!");
    Console.WriteLine("please put a world file in the /worlds folder so the server may load it!");
    Console.WriteLine("Press any key to exit");

    Console.ReadKey();

    _exitEvent.Set(); // allow the process to end!
    return;
}

// get all the spawn points for fish!
List<Vector3> fish_points = WFSermver.ReadWorldFile.readPoints("fish_spawn", File.ReadAllText(worldFile));
List<Vector3> trash_points = WFSermver.ReadWorldFile.readPoints("trash_point", File.ReadAllText(worldFile));
List<Vector3> shoreline_points = WFSermver.ReadWorldFile.readPoints("shoreline_point", File.ReadAllText(worldFile));

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
            LobbyCode = config[key];
            break;

        case "codeOnly":
            {
                if (config[key].ToLower() == "true")
                {
                    codeOnly = true;
                } else if (config[key].ToLower() == "false")
                {
                    codeOnly = false;
                } else
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

void readAdmins()
{
    Dictionary<string, string> config = ConfigReader.ReadConfig("admins.cfg");

    Admins.Clear();

    foreach (string key in config.Keys)
    {
        if (config[key].ToLower() == "true")
        {
            Console.WriteLine($"Added {key} as admin!");
            Admins.Add(key);
            WebFisher player = AllPlayers.Find(p => p.SteamId.Value.ToString() == key);
            if (player != null)
            {
                messagePlayer("You are an admin on this server!", player.SteamId);
            }
        }
    }

}

Console.WriteLine("Reading admins!");
readAdmins();
Console.WriteLine("Setup finished, starting server!");


List<WFInstance> serverOwnedInstances = new();
Steamworks.Data.Lobby gameLobby = new Steamworks.Data.Lobby();

try
{
    SteamClient.Init(3146520, false);
} catch( SystemException e) {
    Console.WriteLine(e.Message);
    return;
}

Dictionary<string, object> readPacket(byte[] packetBytes)
{
    return (new GodotPacketDeserializer(packetBytes)).readPacket();
}

byte[] writePacket(Dictionary<string, object> packet)
{
    byte[] godotBytes = GodotWriter.WriteGodotPacket(packet);
    return GzipHelper.CompressGzip(godotBytes);
}

Thread cbThread = new Thread(runSteamworksUpdate);
cbThread.IsBackground = true;
cbThread.Start();

static void runSteamworksUpdate()
{
    while (true)
    {
        //Console.WriteLine("Update!");
        SteamClient.RunCallbacks();
    }
}

void OnNetworkPacket(P2Packet packet)
{
    Dictionary<string, object> packetInfo = readPacket(GzipHelper.DecompressGzip(packet.Data));

    if ((string)packetInfo["type"] == "handshake_request")
    {
        Dictionary<string, object> handshakePacket = new();
        handshakePacket["type"] = "handshake";
        handshakePacket["user_id"] = SteamClient.SteamId.Value.ToString();

        // send the ping packet!
        SteamNetworking.SendP2PPacket(packet.SteamId, writePacket(handshakePacket), nChannel: 2);
    }

    // tell the client who actualy owns the session!
    if ((string)packetInfo["type"] == "new_player_join")
    {
        messagePlayer("[color=#000000]This is a [b]Cove[/b] dedicated server![/color]", packet.SteamId);
        messagePlayer("[color=#000000]Please report any issues to the [u][url=https://github.com/DrMeepso/WebFishingCove]github![/url][/u][/color]", packet.SteamId);

        Dictionary<string, object> hostPacket = new();
        hostPacket["type"] = "recieve_host";
        hostPacket["host_id"] = SteamClient.SteamId.Value.ToString();

        sendPacketToPlayers(hostPacket);

        string LetterBody = "Cove is still in a very early state and there will be bugs!\n" +
            "The server may crash, but im trying my best to make it stable!\n" +
            "if you encounter a bug or issue please make an issue on the github page so i can fix it!\n" +
            "Github > https://xr0.xyz/cove";

        SendLetter(packet.SteamId, SteamClient.SteamId, "About Cove (The server)", LetterBody, "Happy fishing! - ", "Fries");

        if (isPlayerAdmin(packet.SteamId))
        {
            messagePlayer("Your a admin on this server!", packet.SteamId);
        }

    }

    if ((string)packetInfo["type"] == "instance_actor" && (string)((Dictionary<string, object>)packetInfo["params"])["actor_type"] == "player")
    {
        WebFisher thisPlayer = AllPlayers.Find(p => p.SteamId.Value == packet.SteamId);

        long actorID = (long)((Dictionary<string, object>)packetInfo["params"])["actor_id"];
        if (thisPlayer == null)
        {
            Console.WriteLine("No fisher found for player instance!");
        }
        else
        {
            thisPlayer.PlayerInstanceID = actorID;
        }
    }

    if ((string)packetInfo["type"] == "actor_update")
    {
        WebFisher thisPlayer = AllPlayers.Find(p => p.PlayerInstanceID == (long)packetInfo["actor_id"]);
        if (thisPlayer != null)
        {
            Dictionary<string, object> data = (Dictionary<string, object>)packetInfo["data"];
            Vector3 position = (Vector3)data["pos"];

            thisPlayer.PlayerPosition = position;
        }
    }

    if ((string)packetInfo["type"] == "request_ping")
    {
        Dictionary<string, object> pongPacket = new();
        pongPacket["type"] = "send_ping";
        pongPacket["time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        pongPacket["from"] = SteamClient.SteamId.Value.ToString();

        // send the ping packet!
        SteamNetworking.SendP2PPacket(packet.SteamId, writePacket(pongPacket), nChannel: 1);
    }

    if ((string)packetInfo["type"] == "actor_action")
    {
        if ((string)packetInfo["action"] == "_sync_create_bubble")
        {
            string Message = (string)((Dictionary<int, object>)packetInfo["params"])[0];
            OnPlayerChat(Message, packet.SteamId);
        }
        if ((string)packetInfo["action"] == "_wipe_actor")
        {
            long actorToWipe = (long)((Dictionary<int, object>)packetInfo["params"])[0];
            WFInstance serverInst = serverOwnedInstances.Find(i => (long)i.InstanceID == actorToWipe);
            if (serverInst != null)
            {
                Console.WriteLine("Removing Server Instance!");
                serverOwnedInstances.Remove(serverInst);
            }
        }
    }

    if ((string)packetInfo["type"] == "instance_actor")
    {
        string type = (string)((Dictionary<string, object>)packetInfo["params"])["actor_type"];
        long actorID = (long)((Dictionary<string, object>)packetInfo["params"])["actor_id"];

        // all actor types that should not be spawned by anyone but the server!
        if (type == "meteor" || type == "fish" || type == "rain")
        {
            WebFisher offendingPlayer = AllPlayers.Find(p => p.SteamId == packet.SteamId);

            // kick the player because the spawned in a actor that only the server should be able to spawn!
            Dictionary<string, object> kickPacket = new Dictionary<string, object>();
            kickPacket["type"] = "kick";

            SteamNetworking.SendP2PPacket(packet.SteamId, writePacket(kickPacket), nChannel: 2);
            
            messageGlobal($"{offendingPlayer.FisherName} was kicked for spawning illegal actors");
        }
    }
}

Thread networkThread = new Thread(RunNetwork);
networkThread.IsBackground = true;
networkThread.Start();

void RunNetwork()
{
    while (true)
    {
        try
        {
            // we are going to check if there are any incoming net packets!
            if (SteamNetworking.IsP2PPacketAvailable(channel: 0))
            {
                Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel: 0);
                if (packet != null)
                {
                    OnNetworkPacket(packet.Value);
                }
            }

            // we are going to check if there are any incoming net packets!
            if (SteamNetworking.IsP2PPacketAvailable(channel: 1))
            {
                Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel: 1);
                if (packet != null)
                {
                    OnNetworkPacket(packet.Value);
                }
            }

            // we are going to check if there are any incoming net packets!
            if (SteamNetworking.IsP2PPacketAvailable(channel: 2))
            {
                Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel: 2);
                if (packet != null)
                {
                    OnNetworkPacket(packet.Value);
                }
            }

        } catch (Exception e)
        {
            Console.WriteLine("-- Error responding to packet! --");
            Console.WriteLine(e.ToString());
        }
    }
}

bool isPlayerAdmin(SteamId id)
{
    string adminSteamID = Admins.Find(a => long.Parse(a) == long.Parse(id.ToString()));
    return adminSteamID is string;
}

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

            case "!spawn":
                if (!isPlayerAdmin(id)) return;
                messagePlayer("spawning!", id);
                spawnRainCloud();
                break;

            case "!spawnfish":
                if (!isPlayerAdmin(id)) return;
                spawnFish();
                break;

            case "!spawnm":
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
                } else
                {
                    Dictionary<string,object> packet = new Dictionary<string,object>();
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
                    } else if (arg == "false")
                    {
                        gameLobby.SetJoinable(false);
                        messagePlayer($"Closed lobby!", id);
                        if (!codeOnly)
                        {
                            gameLobby.SetData("type", "code_only");
                            messagePlayer($"Hid server from server list", id);
                        }
                    } else
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
        }
    }
}

void spawnRainCloud()
{
    Random rand = new Random();
    Dictionary<string, object> rainSpawnPacket = new Dictionary<string, object>();

    rainSpawnPacket["type"] = "instance_actor";

    int IId = new Random().Next();

    Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
    rainSpawnPacket["params"] = instanceSpacePrams;

    instanceSpacePrams["actor_type"] = "raincloud";
    instanceSpacePrams["at"] = new Vector3(rand.Next(-100,150), 42, rand.Next(-150, 100));
    instanceSpacePrams["zone"] = "main_zone";
    instanceSpacePrams["actor_id"] = IId;
    instanceSpacePrams["creator_id"] = (string)SteamClient.SteamId.Value.ToString();
    instanceSpacePrams["data"] = new Dictionary<string, object>();

    sendPacketToPlayers(rainSpawnPacket); // spawn the rain!
    serverOwnedInstances.Add(new WFInstance(IId, "raincloud"));
}

void spawnFish(string fishType = "fish_spawn")
{
    Dictionary<string, object> spawnPacket = new Dictionary<string, object>();

    spawnPacket["type"] = "instance_actor";

    int IId = new Random().Next();

    Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
    spawnPacket["params"] = instanceSpacePrams;

    instanceSpacePrams["actor_type"] = fishType;
    instanceSpacePrams["at"] = fish_points[(new Random()).Next(fish_points.Count - 1)];
    instanceSpacePrams["zone"] = "main_zone";
    instanceSpacePrams["actor_id"] = IId;
    instanceSpacePrams["creator_id"] = (string)SteamClient.SteamId.Value.ToString();
    instanceSpacePrams["data"] = new Dictionary<string, object>();

    sendPacketToPlayers(spawnPacket); // spawn the rain!
    serverOwnedInstances.Add(new WFInstance(IId, fishType));
}

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

// returns the letter id!
string SendLetter(SteamId to, SteamId from, string header, string body, string closing, string user)
{
    // Crashes the game lmao
    Dictionary<string, object> letterPacket = new();
    letterPacket["type"] = "letter_received";
    letterPacket["to"] = (double)to.Value;
    Dictionary<string, object> data = new Dictionary<string, object>();
    data["to"] = (double)to;
    data["from"] = (double)from;
    data["header"] = header;
    data["body"] = body;
    data["closing"] = closing;
    data["user"] = user;
    data["letter_id"] = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
    data["items"] = new Dictionary<int, object>();
    letterPacket["data"] = data;

    SteamNetworking.SendP2PPacket(to, writePacket(letterPacket), nChannel: 2);

    return (string)data["letter_id"];
}

void sendPacketToPlayers(Dictionary<string, object> packet)
{
    byte[] packetBytes = writePacket(packet);
    // get all players in the lobby
    foreach (Friend member in gameLobby.Members)
    {
        if (member.Id == SteamClient.SteamId.Value) continue;
        SteamNetworking.SendP2PPacket(member.Id, packetBytes, nChannel: 2);
    }
}

void messageGlobal(string msg)
{
    Dictionary<string, object> chatPacket = new();
    chatPacket["type"] = "message";
    chatPacket["local"] = false;
    chatPacket["sender"] = SteamClient.SteamId.Value.ToString();
    chatPacket["message"] = msg;

    // get all players in the lobby
    foreach (Friend member in gameLobby.Members)
    {
        if (member.Id == SteamClient.SteamId.Value) continue;
        SteamNetworking.SendP2PPacket(member.Id, writePacket(chatPacket), nChannel: 2);
    }
}

void messagePlayer(string msg, SteamId id)
{
    Dictionary<string, object> chatPacket = new();
    chatPacket["type"] = "message";
    chatPacket["local"] = false;
    chatPacket["sender"] = SteamClient.SteamId.Value.ToString();
    chatPacket["message"] = msg;

    SteamNetworking.SendP2PPacket(id, writePacket(chatPacket), nChannel: 2);
}

// request player pings
var messageTimer = new System.Timers.Timer(1000); // An update rate of 5 seconds
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

var hostSpawnTimer = new Repeat(hostSpawn, 10000);
// port of the _host_spawn_object(): in the world.gd script from the game!
int hostSpawn()
{
    // send the host packet again just to make sure!
    Dictionary<string, object> hostPacket = new();
    hostPacket["type"] = "recieve_host";
    hostPacket["host_id"] = SteamClient.SteamId.Value.ToString();

    sendPacketToPlayers(hostPacket);

    // remove old instances!
    foreach (WFInstance inst in serverOwnedInstances)
    {
        float instanceAge = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - inst.SpawnTime.ToUnixTimeSeconds();
        if (inst.Type == "meteor" || instanceAge > 120)
        {
            serverOwnedInstances.Remove(inst);
        }
        if (inst.Type == "fish" || instanceAge > 80)
        {
            serverOwnedInstances.Remove(inst);
        }
        if (inst.Type == "raincloud" || instanceAge > 540)
        {
            serverOwnedInstances.Remove(inst);
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
    } else
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

    //Console.WriteLine($"Current host instance count: {serverOwnedInstances.Count}");

    return 0;
}
hostSpawnTimer.Start();
hostSpawn();


void updatePlayercount()
{
    string serverName = $"{ServerName} [color=#b48141]({gameLobby.MemberCount-1}/{MaxPlayers})[/color] [Dedicated]\n";
    gameLobby.SetData("name", serverName); // not sure what this dose rn

    Console.Title = $"Cove Dedicated Server, {gameLobby.MemberCount-1} players!";
}

SteamMatchmaking.OnLobbyCreated += SteamMatchmaking_OnLobbyCreated;
void SteamMatchmaking_OnLobbyCreated(Result result, Steamworks.Data.Lobby Lobby)
{
    Lobby.SetJoinable(true); // make the server joinable to players!
    Lobby.SetData("mode", "GodotsteamLobby");
    Lobby.SetData("ref", "webfishinglobby");
    Lobby.SetData("version", WebFishingGameVersion);
    Lobby.SetData("code", LobbyCode);
    //Lobby.SetData("type", "public");
    Lobby.SetData("type", codeOnly ? "code_only" : "public");
    Lobby.SetData("public", "true");
    Lobby.SetData("banned_players", "");
    Lobby.SetData("lurefilter", "dedicated"); // make the server showup in lure's dedicated section!

    SteamNetworking.AllowP2PPacketRelay(true);

    Lobby.SetData("server_browser_value", "1"); // i have no idea!

    Console.WriteLine("Lobby Created!");
    Console.WriteLine($"Lobby Code: {Lobby.GetData("code")}");

    gameLobby = Lobby;

    // set the player count in the title
    updatePlayercount();
}

SteamMatchmaking.OnLobbyMemberJoined += SteamMatchmaking_OnLobbyMemberJoined;
void SteamMatchmaking_OnLobbyMemberJoined(Steamworks.Data.Lobby Lobby, Friend userJoining)
{
    Console.WriteLine($"{userJoining.Name} [{userJoining.Id}] has joined the game!");
    updatePlayercount();

    WebFisher newPlayer = new WebFisher(userJoining.Id, userJoining.Name);
    AllPlayers.Add(newPlayer);

    Console.WriteLine($"{userJoining.Name} has been assigned the fisherID: {newPlayer.FisherID}");
}

SteamMatchmaking.OnLobbyMemberLeave += SteamMatchmaking_OnLobbyMemberLeave;

void SteamMatchmaking_OnLobbyMemberLeave(Steamworks.Data.Lobby Lobby, Friend userLeaving)
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
}

SteamNetworking.OnP2PSessionRequest += void (SteamId id) => {
    // because this is what webfishing dose, we are going to allow all connections!

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

SteamMatchmaking.CreateLobbyAsync(maxMembers: MaxPlayers);


Console.CancelKeyPress += Console_CancelKeyPress;
void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    Console.WriteLine("Application is closing...");

    Dictionary<string, object> closePacket = new();
    closePacket["type"] = "server_close";

    // get all players in the lobby
    foreach (Friend member in gameLobby.Members)
    {
        if (member.Id == SteamClient.SteamId.Value) continue;
        SteamNetworking.SendP2PPacket(member.Id, writePacket(closePacket), nChannel: 2);
    }

    gameLobby.Leave(); // close the lobby
    SteamClient.Shutdown();
}

_exitEvent.WaitOne(); // have this at the end of the program, it stops the thread from ending!