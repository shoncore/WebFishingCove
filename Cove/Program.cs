using Cove;
using Cove.Server;
using Steamworks;

ManualResetEvent _exitEvent = new ManualResetEvent(false);
CoveServer webfishingServer = new CoveServer();

webfishingServer.Init(); // start the server

Console.CancelKeyPress += Console_CancelKeyPress;
void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    Console.WriteLine("Application is closing...");

    Dictionary<string, object> closePacket = new();
    closePacket["type"] = "server_close";

    webfishingServer.disconnectAllPlayers();
    webfishingServer.gameLobby.Leave(); // close the lobby
    SteamClient.Shutdown(); // if we are on
}

_exitEvent.WaitOne(); // have this at the end of the program, it stops the thread from ending!