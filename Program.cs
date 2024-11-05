using Steamworks;
using Steamworks.Data;
using System.Threading;


var _exitEvent = new ManualResetEvent(false);
var WebFishingGameVersion = "1.08";
int MaxPlayers = 50;
string ServerName = "Always Fishing 24/7!";
string LobbyCode = "fish";

string[] Admins = new string[2];
Admins[0] = "76561199177316289"; // Uhh

// list of all WebFishers
List<WebFisher> AllPlayers = new();

// get all the spawn points for fish!
List<Vector3> fish_points = WFSermver.ReadWorldFile.readPoints("fish_spawn", File.ReadAllText($"{AppDomain.CurrentDomain.BaseDirectory}worlds/main_zone.tscn"));

try {
    SteamClient.Init(3146520, false);
} catch( SystemException e) {
    Console.WriteLine(e.Message);
    return;
}

// setup a steamworks update timer!
var steamworksTimer = new System.Timers.Timer(1); // An update rate of 120hz
Steamworks.Data.Lobby gameLobby = new Steamworks.Data.Lobby();

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

Thread networkThread = new Thread(RunNetwork);
networkThread.IsBackground = true;
networkThread.Start();

void RunNetwork()
{
    while (true)
    {
        // we are going to check if there are any incoming net packets!
        if (SteamNetworking.IsP2PPacketAvailable(channel: 0))
        {
            Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel: 0);
            if (packet != null)
            {
                Dictionary<string, object> packetInfo = readPacket(GzipHelper.DecompressGzip(packet.Value.Data));

                if ((string)packetInfo["type"] == "handshake_request")
                {
                    Dictionary<string, object> handshakePacket = new();
                    handshakePacket["type"] = "handshake";
                    handshakePacket["user_id"] = SteamClient.SteamId.Value.ToString();

                    // send the ping packet!
                    SteamNetworking.SendP2PPacket(packet.Value.SteamId, writePacket(handshakePacket), nChannel: 2);
                }

                // tell the client who actualy owns the session!
                if ((string)packetInfo["type"] == "new_player_join")
                {
                    Dictionary<string, object> hostPacket = new();
                    hostPacket["type"] = "recieve_host";
                    hostPacket["host_id"] = SteamClient.SteamId.Value.ToString();

                    SendAllPlayer(hostPacket);

                    SendPlayerChat("[color=#000000][u]This server is running a prerelease version of Cove[/u][/color]", packet.Value.SteamId);
                    SendPlayerChat("[color=#000000][u]Cove is a community mod, it is unstable right now![/u][/color]", packet.Value.SteamId);

                    Console.WriteLine("Sending player welcome message");
                }

                if ((string)packetInfo["type"] == "instance_actor" && (string)((Dictionary<string, object>)packetInfo["params"])["actor_type"] == "player")
                {
                    Console.WriteLine("Player has created player Instance!");
                    WebFisher thisPlayer = AllPlayers.Find(p => p.SteamId.Value == packet.Value.SteamId);

                    long actorID = (long)((Dictionary<string, object>)packetInfo["params"])["actor_id"];
                    if (thisPlayer == null)
                    {
                        Console.WriteLine("No fisher found for player instance!");
                    }
                    else
                    {
                        thisPlayer.PlayerInstanceID = actorID;
                        Console.WriteLine($"{thisPlayer.FisherName} has the player instance {thisPlayer.PlayerInstanceID}");
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
            }
        }

        // we are going to check if there are any incoming net packets!
        if (SteamNetworking.IsP2PPacketAvailable(channel: 1))
        {
            Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel: 1);
            if (packet != null)
            {
                Dictionary<string, object> packetInfo = readPacket(GzipHelper.DecompressGzip(packet.Value.Data));

                //Console.WriteLine($"1 > '{packetInfo["type"]}'");

                if ((string)packetInfo["type"] == "request_ping")
                {
                    Dictionary<string, object> pongPacket = new();
                    pongPacket["type"] = "send_ping";
                    pongPacket["time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                    pongPacket["from"] = SteamClient.SteamId.Value.ToString();

                    // send the ping packet!
                    SteamNetworking.SendP2PPacket(packet.Value.SteamId, writePacket(pongPacket), nChannel: 1);

                    Dictionary<string, object> pingPacket = new();
                    pingPacket["type"] = "request_ping";
                    pingPacket["sender"] = SteamClient.SteamId.Value.ToString();

                    // send the ping packet!
                    SteamNetworking.SendP2PPacket(packet.Value.SteamId, writePacket(pingPacket), nChannel: 0);
                }
            }
        }

        // we are going to check if there are any incoming net packets!
        if (SteamNetworking.IsP2PPacketAvailable(channel: 2))
        {
            Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel: 2);
            if (packet != null)
            {
                Dictionary<string, object> packetInfo = readPacket(GzipHelper.DecompressGzip(packet.Value.Data));

                //Console.WriteLine($"2 > '{packetInfo["type"]}'");

                if ((string)packetInfo["type"] == "actor_action")
                {
                    //Console.WriteLine("--- Print Start ---");
                    //printStringDict(packetInfo);
                    //Console.WriteLine("--- Print End ---");
                    if ((string)packetInfo["action"] == "_sync_create_bubble")
                    {
                        string Message = (string)((Dictionary<int, object>)packetInfo["params"])[0];
                        OnPlayerChat(Message, packet.Value.SteamId);
                    }
                }

            }
        }

    }

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

                string messageBody = "";
                foreach (var player in AllPlayers)
                {
                    messageBody += $"{player.FisherName} [{player.SteamId}]: {player.FisherID}\n";
                }

                SendLetter(id, SteamClient.SteamId, "header", messageBody, "yours ", "Cove");

                break;

            case "!spawn":
                SendPlayerChat("spawning!", id);
                spawnRainCloud();
                break;

            case "!spawnfish":
                spawnFish();
                break;

            case "!spawnm":
                spawnFish("fish_spawn_alien"); // metetore
                break;
        }
    }
}

void spawnRainCloud()
{
    Random rand = new Random();
    Dictionary<string, object> rainSpawnPacket = new Dictionary<string, object>();

    rainSpawnPacket["type"] = "instance_actor";

    // {"actor_type": actor_type, "at": at, "zone": zone, "actor_id": id, "creator_id": creator, "data": data}

    Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
    rainSpawnPacket["params"] = instanceSpacePrams;

    instanceSpacePrams["actor_type"] = "raincloud";
    instanceSpacePrams["at"] = new Vector3(rand.Next(-100,150), 42, rand.Next(-150, 100));
    instanceSpacePrams["zone"] = "main_zone";
    instanceSpacePrams["actor_id"] = -1;
    instanceSpacePrams["creator_id"] = (string)SteamClient.SteamId.Value.ToString();
    instanceSpacePrams["data"] = new Dictionary<string, object>();

    SendAllPlayer(rainSpawnPacket); // spawn the rain!
}

void spawnFish(string fishType = "fish_spawn")
{
    Dictionary<string, object> spawnPacket = new Dictionary<string, object>();

    spawnPacket["type"] = "instance_actor";

    // {"actor_type": actor_type, "at": at, "zone": zone, "actor_id": id, "creator_id": creator, "data": data}

    Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
    spawnPacket["params"] = instanceSpacePrams;

    instanceSpacePrams["actor_type"] = fishType;
    instanceSpacePrams["at"] = new Vector3(28.45f, 1.75f, -0.093f); //fish_points[(new Random()).Next(fish_points.Count - 1)];
    instanceSpacePrams["zone"] = "main_zone";
    instanceSpacePrams["actor_id"] = -1;
    instanceSpacePrams["creator_id"] = (string)SteamClient.SteamId.Value.ToString();
    instanceSpacePrams["data"] = new Dictionary<string, object>();

    SendAllPlayer(spawnPacket); // spawn the rain!
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

/*
steamworksTimer.Elapsed += SteamworksTimer_Elapsed;
steamworksTimer.AutoReset = true;
steamworksTimer.Enabled = true; // start the timer!
steamworksTimer.Start();
*/

// returns the letter id!
string SendLetter(SteamId to, SteamId from, string header, string body, string closing, string user)
{
    // Crashes the game lmao
    Dictionary<string, object> letterPacket = new();
    letterPacket["type"] = "letter_received";
    letterPacket["to"] = (double)to.Value;
    Dictionary<string, object> data = new Dictionary<string, object>();
    data["to"] = (double)to.Value;
    data["from"] = (double)from.Value;
    data["header"] = header;
    data["body"] = body;
    data["closing"] = closing;
    data["user"] = user;
    data["letter_id"] = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
    data["items"] = new Dictionary<int, object>();
    letterPacket["data"] = data;

    SteamNetworking.SendP2PPacket(to.AccountId, writePacket(letterPacket), nChannel: 2);

    return (string)data["letter_id"];
}

void SendAllPlayer(Dictionary<string, object> packet)
{
    byte[] packetBytes = writePacket(packet);
    // get all players in the lobby
    foreach (Friend member in gameLobby.Members)
    {
        if (member.Id == SteamClient.SteamId.Value) continue;
        SteamNetworking.SendP2PPacket(member.Id, packetBytes, nChannel: 2);
    }
}

void SendGlobalChat(string msg)
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

void SendPlayerChat(string msg, SteamId id)
{
    Dictionary<string, object> chatPacket = new();
    chatPacket["type"] = "message";
    chatPacket["local"] = false;
    chatPacket["sender"] = SteamClient.SteamId.Value.ToString();
    chatPacket["message"] = msg;

    SteamNetworking.SendP2PPacket(id, writePacket(chatPacket), nChannel: 2);
}

var messageTimer = new System.Timers.Timer(1000); // An update rate of 5 seconds
messageTimer.Elapsed += MessageTimer_Elapsed;
void MessageTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
{
    Dictionary<string, object> pongPacket = new();
    pongPacket["type"] = "send_ping";
    pongPacket["time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    pongPacket["from"] = SteamClient.SteamId.Value.ToString();

    SendAllPlayer(pongPacket);

    Dictionary<string, object> pingPacket = new();
    pingPacket["type"] = "request_ping";
    pingPacket["sender"] = SteamClient.SteamId.Value.ToString();

    SendAllPlayer(pingPacket);

    Dictionary<string, object> hostPacket = new();
    hostPacket["type"] = "recieve_host";
    hostPacket["host_id"] = SteamClient.SteamId.Value.ToString();

    SendAllPlayer(hostPacket);
}
messageTimer.AutoReset = true;
messageTimer.Enabled = true; // start the timer!
messageTimer.Start();

void updatePlayercount()
{
    string serverName = $"{ServerName} [color=#b48141]({gameLobby.MemberCount-1}/{MaxPlayers})[/color] [Dedicated]\n";
    gameLobby.SetData("name", serverName); // not sure what this dose rn
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
    Lobby.SetData("type", "code_only");
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
    
    Console.WriteLine($"Player count: {gameLobby.MemberCount - 1}");
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

    Console.WriteLine($"Player count: {gameLobby.MemberCount - 1}");
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