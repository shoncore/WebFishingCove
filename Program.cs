using Steamworks;
using Steamworks.Data;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Net.Sockets;
using System.Security.Cryptography;
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
            LobbyCode = config[key].ToUpper();
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
}
catch ( SystemException e) {
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

void runSteamworksUpdate()
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

    //printStringDict(packetInfo);

    if ((string)packetInfo["type"] == "send_ping")
    {
        //printStringDict(packetInfo);
    }

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
        printStringDict(packetInfo);

        messagePlayer("This is a Cove dedicated server!", packet.SteamId);
        messagePlayer("Please report any issues to the github (xr0.xyz/cove)", packet.SteamId);

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
            messagePlayer("You're an admin on this server!", packet.SteamId);
        }

        //spawnServerPlayerActor(packet.SteamId);
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
            Vector3 position = (Vector3)packetInfo["pos"];
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
                Console.WriteLine($"Player asked to remove {serverInst.Type} actor");

                // the sever owns the instance
                Console.WriteLine("Removing Server Instance!");
                removeServerActor(serverInst);
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

        } catch (Exception e)
        {
            Console.WriteLine("-- Error responding to packet! --");
            Console.WriteLine(e.ToString());
        }
    }
}

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

Repeat clientUpdateRate = new Repeat(actorUpdate, 1000/12); // 8 updates per second should be more than enough!
clientUpdateRate.Start();

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

void spawnRainCloud()
{
    Random rand = new Random();
    Dictionary<string, object> rainSpawnPacket = new Dictionary<string, object>();

    rainSpawnPacket["type"] = "instance_actor";

    int IId = new Random().Next();

    Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
    rainSpawnPacket["params"] = instanceSpacePrams;

    Vector3 pos = new Vector3(rand.Next(-100, 150), 42f, rand.Next(-150, 100));

    instanceSpacePrams["actor_type"] = "raincloud";
    instanceSpacePrams["at"] = pos;
    instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
    instanceSpacePrams["zone"] = "main_zone";
    instanceSpacePrams["zone_owner"] = -1;
    instanceSpacePrams["actor_id"] = IId;
    instanceSpacePrams["creator_id"] = (long)SteamClient.SteamId.Value;

    sendPacketToPlayers(rainSpawnPacket); // spawn the rain!
    serverOwnedInstances.Add(new RainCloud(IId, pos));
}

void spawnFish(string fishType = "fish_spawn")
{
    Dictionary<string, object> spawnPacket = new Dictionary<string, object>();

    spawnPacket["type"] = "instance_actor";

    int IId = new Random().Next();

    Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
    spawnPacket["params"] = instanceSpacePrams;

    Vector3 pos = fish_points[(new Random()).Next(fish_points.Count - 1)];

    instanceSpacePrams["actor_type"] = fishType;
    instanceSpacePrams["at"] = pos;
    instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
    instanceSpacePrams["zone"] = "main_zone";
    instanceSpacePrams["zone_owner"] = -1;
    instanceSpacePrams["actor_id"] = IId;
    instanceSpacePrams["creator_id"] = (long)SteamClient.SteamId.Value;

    sendPacketToPlayers(spawnPacket); // spawn the rain!
    serverOwnedInstances.Add(new WFInstance(IId, fishType, pos));
}

void spawnServerPlayerActor(SteamId id)
{
    Dictionary<string, object> spawnPacket = new Dictionary<string, object>();

    spawnPacket["type"] = "instance_actor";

    Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
    spawnPacket["params"] = instanceSpacePrams;


    instanceSpacePrams["actor_type"] = "player";
    instanceSpacePrams["at"] = Vector3.zero;
    instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
    instanceSpacePrams["zone"] = "main_zone";
    instanceSpacePrams["zone_owner"] = -1;
    instanceSpacePrams["actor_id"] = 255;
    instanceSpacePrams["creator_id"] = (long)SteamClient.SteamId.Value;

    SteamNetworking.SendP2PPacket(id, writePacket(spawnPacket), nChannel: 2);

    Console.WriteLine("Spawned server player actor for player!");
    serverOwnedInstances.Add(new WFInstance(255, "player", Vector3.zero));

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

// returns the letter id!
int SendLetter(SteamId to, SteamId from, string header, string body, string closing, string user)
{
    // Crashes the game lmao
    Dictionary<string, object> letterPacket = new();
    letterPacket["type"] = "letter_received";
    letterPacket["to"] = (string)to.Value.ToString();
    Dictionary<string, object> data = new Dictionary<string, object>();
    data["to"] = (string)to.Value.ToString();
    data["from"] = (double)from;
    data["header"] = header;
    data["body"] = body;
    data["closing"] = closing;
    data["user"] = user;
    data["letter_id"] = new Random().Next();
    data["items"] = new Dictionary<int, object>();
    letterPacket["data"] = data;

    SteamNetworking.SendP2PPacket(to, writePacket(letterPacket), nChannel: 2);

    return (int)data["letter_id"];
}

void messageGlobal(string msg, string color = "ffffff")
{
    Dictionary<string, object> chatPacket = new();
    chatPacket["type"] = "message";
    chatPacket["message"] = msg;
    chatPacket["color"] = color;
    chatPacket["local"] = false;
    chatPacket["position"] = new Vector3(0f, 0f, 0f);
    chatPacket["zone"] = "main_zone";
    chatPacket["zone_owner"] = 1;

    // get all players in the lobby
    foreach (Friend member in gameLobby.Members)
    {
        if (member.Id == SteamClient.SteamId.Value) continue;
        SteamNetworking.SendP2PPacket(member.Id, writePacket(chatPacket), nChannel: 2);
    }
}

void messagePlayer(string msg, SteamId id, string color = "ffffff")
{
    Dictionary<string, object> chatPacket = new();
    chatPacket["type"] = "message";
    chatPacket["message"] = msg;
    chatPacket["color"] = color;
    chatPacket["local"] = (bool)false;
    chatPacket["position"] = new Vector3(0f, 0f, 0f);
    chatPacket["zone"] = "main_zone";
    chatPacket["zone_owner"] = 1;

    SteamNetworking.SendP2PPacket(id, writePacket(chatPacket), nChannel: 2);
}

void removeServerActor(WFInstance instance)
{
    Dictionary<string, object> removePacket = new();
    removePacket["type"] = "actor_action";
    removePacket["actor_id"] = instance.InstanceID;
    removePacket["action"] = "queue_free";

    Dictionary<int, object> prams = new Dictionary<int, object>();
    removePacket["params"] = prams;

    sendPacketToPlayers(removePacket); // remove

    serverOwnedInstances.Remove(instance);
}

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

var hostSpawnTimer = new Repeat(hostSpawn, 10000);
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
    string serverName = $"{ServerName} ({gameLobby.MemberCount-1}/{MaxPlayers}) [Dedicated]\n";
    gameLobby.SetData("lobby_name", serverName); // not sure what this dose rn

    Console.Title = $"Cove Dedicated Server, {gameLobby.MemberCount-1} players!";
}

SteamMatchmaking.OnLobbyCreated += SteamMatchmaking_OnLobbyCreated;
void SteamMatchmaking_OnLobbyCreated(Result result, Steamworks.Data.Lobby Lobby)
{
    Lobby.SetJoinable(true); // make the server joinable to players!
    Lobby.SetData("ref", "webfishing_gamelobby");
    Lobby.SetData("name", SteamClient.Name);
    Lobby.SetData("version", WebFishingGameVersion);
    Lobby.SetData("code", LobbyCode);
    Lobby.SetData("type", codeOnly ? "code_only" : "public");
    Lobby.SetData("public", "true");
    Lobby.SetData("banned_players", "");
    Lobby.SetData("age_limit", "false");
    Lobby.SetData("cap", MaxPlayers.ToString());
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
