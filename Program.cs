using Steamworks;
using System.Collections.Generic;
using System.Text;

var _exitEvent = new ManualResetEvent(false);
var WebFishingGameVersion = "1.08";

try {
    SteamClient.Init(3146520, true);
} catch( SystemException e) {
    Console.WriteLine(e.Message);
    return;
}

// setup a steamworks update timer!
var steamworksTimer = new System.Timers.Timer(1000/60); // An update rate of 60hz
Steamworks.Data.Lobby gameLobby = new Steamworks.Data.Lobby();

void SteamworksTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
{
    SteamClient.RunCallbacks();

    // we are going to check if there are any incoming net packets!
    if (SteamNetworking.IsP2PPacketAvailable(channel: 2))
    {
        Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel: 2);
        if (packet != null)
        {
            byte[] packetBytes = GzipHelper.DecompressGzip(packet.Value.Data);

            GodotPacketDeserializer packetDeserializer = new GodotPacketDeserializer(packetBytes);
            var packetValues = packetDeserializer.readPacket();

            printStringDict(packetValues);

            packetValues = null;

        }
    }
}

void printStringDict(Dictionary<string, object> obj, string sub = "")
{
    Console.WriteLine("--- Print Start ---");
    foreach (var kvp in obj)
    {
        if (kvp.Value is Dictionary<string, object>)
        {
            printStringDict((Dictionary<string, object>) kvp.Value, sub + "." + kvp.Key);
        } else
        {
            Console.WriteLine($"{sub} {kvp.Key}: {kvp.Value}");
        }
    }
    Console.WriteLine("--- Print End ---");
}

steamworksTimer.Elapsed += SteamworksTimer_Elapsed;
steamworksTimer.AutoReset = true;
steamworksTimer.Enabled = true; // start the timer!
steamworksTimer.Start();

SteamMatchmaking.OnLobbyCreated += SteamMatchmaking_OnLobbyCreated;
void SteamMatchmaking_OnLobbyCreated(Result result, Steamworks.Data.Lobby Lobby)
{
    Lobby.SetJoinable(true); // make the server joinable to players!
    Lobby.SetData("name", SteamClient.Name); // not sure what this dose rn
    Lobby.SetData("mode", "GodotsteamLobby");
    Lobby.SetData("ref", "webfishinglobby");
    Lobby.SetData("version", WebFishingGameVersion);
    Lobby.SetData("code", "fish");
    Lobby.SetData("type", "public");
    Lobby.SetData("public", "true");
    Lobby.SetData("banned_players", "");

    SteamNetworking.AllowP2PPacketRelay(true);

    Lobby.SetData("server_browser_value", "0"); // i have no idea!

    Console.WriteLine("Lobby Created!");
    Console.WriteLine("Lobby Code: fish");

    gameLobby = Lobby;
}

SteamMatchmaking.OnLobbyMemberJoined += SteamMatchmaking_OnLobbyMemberJoined;
void SteamMatchmaking_OnLobbyMemberJoined(Steamworks.Data.Lobby Lobby, Friend userJoining)
{
    Console.WriteLine(userJoining.Name + " has joined the game!");
}

SteamNetworking.OnP2PSessionRequest += void (SteamId id) => {
    // because this is what webfishing dose, we are going to allow all connections!
    SteamNetworking.AcceptP2PSessionWithUser(id);
};

SteamMatchmaking.CreateLobbyAsync(5);

_exitEvent.WaitOne(); // have this at the end of the program, it stops the thread from ending!