﻿namespace Cove.Server
{
    public partial class CoveServer
    {
        private readonly HashSet<string> _loggedUnknownPacketTypes = [];

        /// <summary>
        /// Handles incoming network packets and performs actions based on the packet type.
        /// </summary>
        /// <param name="packet">The incoming P2P network packet.</param>
        private void OnNetworkPacket(P2Packet packet)
        {
            var steamId = packet.SteamId;
            if (IsPlayerBanned(steamId))
            {
                Logger.LogInformation("Player {SteamId} is banned. Kicking...", steamId.Value);
                KickPlayer(steamId);
            }

            var packetInfo = ParsePacket(packet);

            if (!packetInfo.TryGetValue("type", out var typeObj) || typeObj is not string type)
            {
                Logger.LogInformation("Invalid packet type.");
                return;
            }

            switch (type)
            {
                case "handshake_request":
                    HandleHandshakeRequest(packet.SteamId);
                    break;

                case "new_player_join":
                    HandleNewPlayerJoin(packet.SteamId);
                    break;

                case "instance_actor":
                    HandleInstanceActor(packetInfo, packet.SteamId);
                    break;

                case "actor_update":
                    HandleActorUpdate(packetInfo, packet.SteamId);
                    break;

                case "request_ping":
                    HandleRequestPing(packet.SteamId);
                    break;

                case "actor_action":
                    HandleActorAction(packetInfo, packet.SteamId);
                    break;

                case "request_actors":
                    HandleRequestActors(packet.SteamId);
                    break;

                default:
                    LogUnknownPacketType(type);
                    break;
            }
        }

        /// <summary>
        /// This method logs a warning for an unhandled packet type only once.
        /// Subsequent packets of the same type will not be logged, preventing log spam.
        /// </summary>
        /// <param name="type"></param>
        private void LogUnknownPacketType(string type)
        {
            if (_loggedUnknownPacketTypes.Add(type))
            {
                Logger.LogWarning("Unhandled packet type: {Type}", type);
            }
        }

        private Dictionary<string, object> ParsePacket(P2Packet packet)
        {
            var decompressedData = GzipHelper.DecompressGzip(packet.Data);
            var packetInfo = ReadPacket(
                decompressedData,
                LoggerFactory.CreateLogger<GodotReader>()
            );
            return packetInfo;
        }

        private void HandleHandshakeRequest(SteamId steamId)
        {
            var handshakePacket = new Dictionary<string, object>
            {
                { "type", "handshake" },
                { "user_id", steamId.Value.ToString() },
            };

            Logger.LogInformation("Sending handshake packet to {SteamId}", steamId.Value);
            SteamNetworking.SendP2PPacket(steamId, WritePacket(handshakePacket), nChannel: 2);
        }

        private void HandleNewPlayerJoin(SteamId steamId)
        {
            if (!HideJoinMessage)
            {
                MessagePlayer($"{ServerName}", steamId);
                MessagePlayer($"{MessageOfTheDay}", steamId);
            }

            var hostPacket = new Dictionary<string, object>
            {
                { "type", "receive_host" },
                { "host_id", SteamClient.SteamId.Value.ToString() }
            };

            SendPacketToPlayers(hostPacket);

            if (IsPlayerAdmin(steamId))
            {
                MessagePlayer("You're an admin on this server.", steamId);
            }
        }

        private void HandleInstanceActor(Dictionary<string, object> packetInfo, SteamId steamId)
        {
            if (
                !packetInfo.TryGetValue("params", out var paramsObj)
                || paramsObj is not Dictionary<string, object> parameters
            )
                return;

            if (
                parameters.TryGetValue("actor_type", out var actorTypeObj)
                && actorTypeObj is string actorType
            )
            {
                if (
                    actorType == "player"
                    && parameters.TryGetValue("actor_id", out var actorIdObj)
                    && actorIdObj is long actorId
                )
                {
                    var player = AllPlayers.Find(p => p.SteamId.Value == steamId.Value);
                    if (player == null)
                    {
                        player = new WFPlayer(steamId, "???");
                        AllPlayers.Add(player);
                    }
                    else
                    {
                        player.InstanceId = actorId;
                    }
                }

                if (IsIllegalActorType(actorType))
                {
                    KickPlayerForIllegalActor(steamId, actorType);
                }
            }
        }

        private void HandleActorUpdate(Dictionary<string, object> packetInfo, SteamId steamId)
        {
            if (
                !packetInfo.TryGetValue("actor_id", out var actorIdObj)
                || actorIdObj is not long actorId
            )
                return;

            var player = AllPlayers.Find(p => p.InstanceId == actorId);
            if (
                player != null
                && packetInfo.TryGetValue("pos", out var posObj)
                && posObj is Vector3 position
            )
            {
                player.Position = position;
            }
        }

        private static void HandleRequestPing(SteamId steamId)
        {
            var pongPacket = new Dictionary<string, object>
            {
                { "type", "send_ping" },
                { "time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
                { "from", SteamClient.SteamId.Value.ToString() }
            };

            SteamNetworking.SendP2PPacket(steamId, WritePacket(pongPacket), nChannel: 1);
        }

        void OnPlayerChat(string message, SteamId steamId)
        {
            // Ensure AllPlayers is not null
            if (AllPlayers == null)
            {
                Logger.LogError("Error: AllPlayers list is null.");
                return;
            }

            // Attempt to find the sender
            Logger.LogWarning("Chat message from {SteamId}", steamId.Value);
            AllPlayers.ForEach(p => Logger.LogWarning("Player: {Player}", p.SteamId.Value));

            WFPlayer? player = AllPlayers.Find(p => p.SteamId.Value == steamId.Value);
            if (player == null)
            {
                Logger.LogError(
                    "Error: Could not find player with SteamId {SteamId}",
                    steamId.Value
                );
                return;
            }

            Logger.LogInformation(
                "{FisherName} ({SteamId}): {Message}",
                player.FisherName,
                steamId.Value,
                message
            );

            // Safely iterate through plugins
            foreach (PluginInstance plugin in LoadedPlugins)
            {
                plugin.plugin.OnChatMessage(player, message);
            }
        }

        private void HandleActorAction(Dictionary<string, object> packetInfo, SteamId steamId)
        {
            if (
                !packetInfo.TryGetValue("action", out var actionObj)
                || actionObj is not string action
            )
                return;

            if (
                action == "_sync_create_bubble"
                && packetInfo.TryGetValue("params", out var paramsObj)
                && paramsObj is Dictionary<int, object> parameters
            )
            {
                if (parameters.TryGetValue(0, out var messageObj) && messageObj is string message)
                {
                    OnPlayerChat(message, steamId);
                }
            }

            if (
                action == "_wipe_actor"
                && packetInfo.TryGetValue("params", out paramsObj)
                && paramsObj is Dictionary<int, object> actorParams
            )
            {
                if (
                    actorParams.TryGetValue(0, out var actorToWipeObj)
                    && actorToWipeObj is long actorToWipe
                )
                {
                    var serverInst = ServerOwnedInstances.Find(i => i.InstanceId == actorToWipe);
                    if (serverInst != null)
                    {
                        Logger.LogInformation(
                            "Player request to remove {Type} Actor",
                            serverInst.Type
                        );
                        RemoveServerActor(serverInst);
                    }
                }
            }
        }

        private void HandleRequestActors(SteamId steamId)
        {
            SendPlayerAllServerActors(steamId);
            SendPacketToPlayer(CreateRequestActorResponse(), steamId);
        }

        private bool IsIllegalActorType(string actorType)
        {
            Logger.LogInformation("Checking actor type: {ActorType}", actorType);
            return actorType switch
            {
                "fish_spawn_alien" => true,
                "fish_spawn" => true,
                "raincloud" => true,
                _ => false
            };
        }

        private void KickPlayerForIllegalActor(SteamId steamId, string actorType)
        {
            var player = AllPlayers.Find(p => p.SteamId.Value == steamId.Value);
            if (player == null)
            {
                Logger.LogInformation("No player found for illegal actor kick!");
                return;
            }

            Logger.LogInformation(
                "Kicking player {FisherName} [{SteamId}] for spawning illegal actor: {ActorType}",
                player.FisherName,
                player.SteamId.Value,
                actorType
            );

            var kickPacket = new Dictionary<string, object> { { "type", "kick" } };
            SendPacketToPlayer(kickPacket, steamId);
            MessageGlobal(
                $"{player?.FisherName ?? "Unknown player name"} [{player?.SteamId.Value.ToString() ?? "Unknown SteamId"}] was kicked for spawning illegal actor: {actorType}"
            );
        }
    }
}
