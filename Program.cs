using Steamworks;
using Steamworks.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using System.Text;

var _exitEvent = new ManualResetEvent(false);
var WebFishingGameVersion = "1.08";
int MaxPlayers = 50;

try {
    SteamClient.Init(3146520, true);
} catch( SystemException e) {
    Console.WriteLine(e.Message);
    return;
}

// setup a steamworks update timer!
var steamworksTimer = new System.Timers.Timer(1000/120); // An update rate of 60hz
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

void SteamworksTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
{
    SteamClient.RunCallbacks();

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


                Console.WriteLine($"User has been informed of the lobby host!");

                SendPlayerChat("[b]The dedicated server is a community mod, it is unstable right now![/b]", packet.Value.SteamId);
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

            if ((string) packetInfo["type"] == "request_ping")
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
            }

        }
    }

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

steamworksTimer.Elapsed += SteamworksTimer_Elapsed;
steamworksTimer.AutoReset = true;
steamworksTimer.Enabled = true; // start the timer!
steamworksTimer.Start();

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
    string serverName = $"24/7 Fishing [color=#b48141]({gameLobby.MemberCount-1}/{MaxPlayers})[/color] [Dedicated]\n";
    gameLobby.SetData("name", serverName); // not sure what this dose rn
}

SteamMatchmaking.OnLobbyCreated += SteamMatchmaking_OnLobbyCreated;
void SteamMatchmaking_OnLobbyCreated(Result result, Steamworks.Data.Lobby Lobby)
{
    Lobby.SetJoinable(true); // make the server joinable to players!
    Lobby.SetData("mode", "GodotsteamLobby");
    Lobby.SetData("ref", "webfishinglobby");
    Lobby.SetData("version", WebFishingGameVersion);
    Lobby.SetData("code", "fish");
    Lobby.SetData("type", "public");
    Lobby.SetData("public", "true");
    Lobby.SetData("banned_players", "");

    SteamNetworking.AllowP2PPacketRelay(true);

    Lobby.SetData("server_browser_value", "2"); // i have no idea!

    Console.WriteLine("Lobby Created!");
    Console.WriteLine("Lobby Code: fish");

    gameLobby = Lobby;

    // set the player count in the title
    updatePlayercount();
}

SteamMatchmaking.OnLobbyMemberJoined += SteamMatchmaking_OnLobbyMemberJoined;
void SteamMatchmaking_OnLobbyMemberJoined(Steamworks.Data.Lobby Lobby, Friend userJoining)
{
    Console.WriteLine(userJoining.Name + " has joined the game!");
    updatePlayercount();
}

SteamMatchmaking.OnLobbyMemberLeave += SteamMatchmaking_OnLobbyMemberLeave;

void SteamMatchmaking_OnLobbyMemberLeave(Steamworks.Data.Lobby Lobby, Friend userLeaving)
{
    Console.WriteLine(userLeaving.Name + " has left the game!");
    updatePlayercount();
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
}

_exitEvent.WaitOne(); // have this at the end of the program, it stops the thread from ending!