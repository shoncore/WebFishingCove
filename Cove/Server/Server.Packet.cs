using Steamworks;
using Steamworks.Data;
using Cove.GodotFormat;
using Cove.Server.Actor;
using Cove.Server.Utils;

namespace Cove.Server
{
    partial class CoveServer
    {
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
                if (!hideJoinMessage)
                {
                    messagePlayer("This is a Cove dedicated server!", packet.SteamId);
                    messagePlayer("Please report any issues to the github (xr0.xyz/cove)", packet.SteamId);
                }

                Dictionary<string, object> hostPacket = new();
                hostPacket["type"] = "recieve_host";
                hostPacket["host_id"] = SteamClient.SteamId.Value.ToString();

                sendPacketToPlayers(hostPacket);

                /*
                string LetterBody = "Cove is still in a very early state and there will be bugs!\n" +
                    "The server may crash, but im trying my best to make it stable!\n" +
                    "if you encounter a bug or issue please make an issue on the github page so i can fix it!\n" +
                    "Github > https://xr0.xyz/cove";

                SendLetter(packet.SteamId, SteamClient.SteamId, "About Cove (The server)", LetterBody, "Happy fishing! - ", "Fries");
                */

                if (isPlayerAdmin(packet.SteamId))
                {
                    messagePlayer("You're an admin on this server!", packet.SteamId);
                }

                //spawnServerPlayerActor(packet.SteamId);
            }

            if ((string)packetInfo["type"] == "instance_actor" && (string)((Dictionary<string, object>)packetInfo["params"])["actor_type"] == "player")
            {
                WFPlayer thisPlayer = AllPlayers.Find(p => p.SteamId.Value == packet.SteamId);

                long actorID = (long)((Dictionary<string, object>)packetInfo["params"])["actor_id"];
                if (thisPlayer == null)
                {
                    Console.WriteLine("No fisher found for player instance!");
                }
                else
                {
                    thisPlayer.InstanceID = actorID;
                }
            }

            if ((string)packetInfo["type"] == "actor_update")
            {
                WFPlayer thisPlayer = AllPlayers.Find(p => p.InstanceID == (long)packetInfo["actor_id"]);
                if (thisPlayer != null)
                {
                    Vector3 position = (Vector3)packetInfo["pos"];
                    thisPlayer.pos = position;
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
                    WFActor serverInst = serverOwnedInstances.Find(i => (long)i.InstanceID == actorToWipe);
                    if (serverInst != null)
                    {
                        Console.WriteLine($"Player asked to remove {serverInst.Type} actor");

                        // the sever owns the instance
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
                    WFPlayer offendingPlayer = AllPlayers.Find(p => p.SteamId == packet.SteamId);

                    // kick the player because the spawned in a actor that only the server should be able to spawn!
                    Dictionary<string, object> kickPacket = new Dictionary<string, object>();
                    kickPacket["type"] = "kick";

                    sendPacketToPlayer(kickPacket, packet.SteamId);

                    messageGlobal($"{offendingPlayer.FisherName} was kicked for spawning illegal actors");
                }
            }

            if ((string)packetInfo["type"] == "request_actors")
            {
                sendPlayerAllServerActors(packet.SteamId);
                sendPacketToPlayer(createRequestActorResponce(), packet.SteamId); // this is empty because otherwise all the server actors are invisible!
            }

        }
    }
}
