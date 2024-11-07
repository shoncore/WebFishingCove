using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WFSermver;

ManualResetEvent _exitEvent = new ManualResetEvent(false);
Server webfishingServer = new Server();

webfishingServer.Init(); // start the server

Console.CancelKeyPress += Console_CancelKeyPress;
void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    Console.WriteLine("Application is closing...");

    Dictionary<string, object> closePacket = new();
    closePacket["type"] = "server_close";

    webfishingServer.disconnectPlayers();
    webfishingServer.gameLobby.Leave(); // close the lobby
    SteamClient.Shutdown(); // if we are on
}

_exitEvent.WaitOne(); // have this at the end of the program, it stops the thread from ending!