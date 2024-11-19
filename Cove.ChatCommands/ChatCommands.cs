using System;
using System.Collections.Generic;
using System.Linq;
using Cove.Server;
using Cove.Server.Actor;
using Cove.Server.Plugins;

namespace Cove.ChatCommands
{
  /// <summary>
  /// Plugin that provides chat command functionality to the Cove Server.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="ChatCommands"/> class.
  /// </remarks>
  /// <param name="server">The CoveServer instance.</param>
  public class ChatCommands(CoveServer server) : CovePlugin(server)
    {
    /// <summary>
    /// Gets or sets the CoveServer instance.
    /// </summary>
    private CoveServer Server { get; set; } = server;

    /// <summary>
    /// Called when the plugin is initialized.
    /// </summary>
    public override void OnInit()
        {
            base.OnInit();
            // Any initialization code for the plugin can go here
        }

        /// <summary>
        /// Called when a player joins the server.
        /// </summary>
        /// <param name="player">The player who joined.</param>
        public override void OnPlayerJoin(WFPlayer player)
        {
            base.OnPlayerJoin(player);
            // Any code to execute when a player joins can go here
        }

        /// <summary>
        /// Called when a chat message is received.
        /// </summary>
        /// <param name="sender">The player who sent the message.</param>
        /// <param name="message">The message content.</param>
        public override void OnChatMessage(WFPlayer sender, string message)
        {
            base.OnChatMessage(sender, message);

            if (string.IsNullOrEmpty(message))
                return;

            // Check if the message is a command (starts with '!')
            if (message.StartsWith('!'))
            {
                // Split the message into command and arguments
                string[] commandParts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string command = commandParts[0].ToLower();

                switch (command)
                {
                    case "!help":
                        HandleHelpCommand(sender);
                        break;

                    case "!users":
                        HandleUsersCommand(sender, commandParts);
                        break;

                    case "!spawn":
                        HandleSpawnCommand(sender, commandParts);
                        break;

                    case "!kick":
                        HandleKickCommand(sender, commandParts);
                        break;

                    case "!ban":
                        HandleBanCommand(sender, commandParts);
                        break;

                    case "!setjoinable":
                        HandleSetJoinableCommand(sender, commandParts);
                        break;

                    case "!refreshadmins":
                        HandleRefreshAdminsCommand(sender);
                        break;

                    default:
                        SendPlayerChatMessage(sender, $"Unknown command: {command}");
                        break;
                }
            }
        }

        /// <summary>
        /// Handles the "!help" command.
        /// </summary>
        /// <param name="sender">The player who issued the command.</param>
        private void HandleHelpCommand(WFPlayer sender)
        {
            SendPlayerChatMessage(sender, "--- HELP ---");
            SendPlayerChatMessage(sender, "!help - Shows this message");
            SendPlayerChatMessage(sender, "!users [page] - Shows all players in the server");
            SendPlayerChatMessage(sender, "!spawn <actor> - Spawns an actor");
            SendPlayerChatMessage(sender, "!kick <player> - Kicks a player");
            SendPlayerChatMessage(sender, "!ban <player> - Bans a player");
            SendPlayerChatMessage(sender, "!setjoinable <true/false> - Opens or closes the lobby");
            SendPlayerChatMessage(sender, "!refreshadmins - Refreshes the admins list");
        }

        /// <summary>
        /// Handles the "!users" command.
        /// </summary>
        /// <param name="sender">The player who issued the command.</param>
        /// <param name="commandParts">The command and its arguments.</param>
        private void HandleUsersCommand(WFPlayer sender, string[] commandParts)
        {
            if (!IsPlayerAdmin(sender)) return;

            int pageNumber = 1;
            int pageSize = 10;

            // Check if a page number was provided
            if (commandParts.Length > 1)
            {
                if (!int.TryParse(commandParts[1], out pageNumber) || pageNumber < 1)
                {
                    pageNumber = 1; // Default to page 1 if parsing fails or page number is less than 1
                }
            }

            var allPlayers = GetAllPlayers();
            int totalPlayers = allPlayers.Length;
            int totalPages = (int)Math.Ceiling((double)totalPlayers / pageSize);

            // Ensure the page number is within the valid range
            pageNumber = Math.Clamp(pageNumber, 1, totalPages);

            // Get the players for the current page
            var playersOnPage = allPlayers.Skip((pageNumber - 1) * pageSize).Take(pageSize);

            // Build the message to send
            var messageBody = new System.Text.StringBuilder();
            messageBody.AppendLine("Players in the server:");
            foreach (var player in playersOnPage)
            {
                messageBody.AppendLine($"{player.FisherName}: {player.FisherID}");
            }

            messageBody.AppendLine($"Page {pageNumber} of {totalPages}");

            SendPlayerChatMessage(sender, messageBody.ToString());
        }

        /// <summary>
        /// Handles the "!spawn" command.
        /// </summary>
        /// <param name="sender">The player who issued the command.</param>
        /// <param name="commandParts">The command and its arguments.</param>
        private void HandleSpawnCommand(WFPlayer sender, string[] commandParts)
        {
            if (!IsPlayerAdmin(sender)) return;

            if (commandParts.Length < 2)
            {
                SendPlayerChatMessage(sender, "Usage: !spawn <actor>");
                return;
            }

            string actorType = commandParts[1].ToLower();
            bool spawned = false;
            switch (actorType)
            {
                case "rain":
                    Server.SpawnRainCloud();
                    spawned = true;
                    break;

                case "fish":
                    Server.SpawnFish();
                    spawned = true;
                    break;

                case "meteor":
                    Server.SpawnFish("fish_spawn_alien");
                    spawned = true;
                    break;

                case "portal":
                    Server.SpawnVoidPortal();
                    spawned = true;
                    break;

                case "metal":
                    Server.SpawnMetal();
                    spawned = true;
                    break;

                default:
                    spawned = false;
                    break;
            }

            if (spawned)
            {
                SendPlayerChatMessage(sender, $"Spawned {actorType}");
            }
            else
            {
                SendPlayerChatMessage(sender, $"\"{actorType}\" is not a spawnable actor!");
            }
        }

        /// <summary>
        /// Handles the "!kick" command.
        /// </summary>
        /// <param name="sender">The player who issued the command.</param>
        /// <param name="commandParts">The command and its arguments.</param>
        private void HandleKickCommand(WFPlayer sender, string[] commandParts)
        {
            if (!IsPlayerAdmin(sender)) return;

            if (commandParts.Length < 2)
            {
                SendPlayerChatMessage(sender, "Usage: !kick <player>");
                return;
            }

            string playerName = string.Join(' ', commandParts.Skip(1));
            WFPlayer? kickedPlayer = GetAllPlayers().FirstOrDefault(p => p.FisherName.Equals(playerName, StringComparison.OrdinalIgnoreCase));

            if (kickedPlayer == null)
            {
                SendPlayerChatMessage(sender, "Player not found!");
            }
            else
            {
                var packet = new Dictionary<string, object>
                {
                    ["type"] = "kick"
                };

                SendPacketToPlayer(packet, kickedPlayer);

                SendPlayerChatMessage(sender, $"Kicked {kickedPlayer.FisherName}");
                SendGlobalChatMessage($"{kickedPlayer.FisherName} was kicked from the lobby!");
            }
        }

        /// <summary>
        /// Handles the "!ban" command.
        /// </summary>
        /// <param name="sender">The player who issued the command.</param>
        /// <param name="commandParts">The command and its arguments.</param>
        private void HandleBanCommand(WFPlayer sender, string[] commandParts)
        {
            if (!IsPlayerAdmin(sender)) return;

            if (commandParts.Length < 2)
            {
                SendPlayerChatMessage(sender, "Usage: !ban <player>");
                return;
            }

            string playerName = string.Join(' ', commandParts.Skip(1));
            WFPlayer playerToBan = GetAllPlayers().FirstOrDefault(p => p.FisherName.Equals(playerName, StringComparison.OrdinalIgnoreCase))!;

            if (playerToBan == null)
            {
                SendPlayerChatMessage(sender, "Player not found!");
            }
            else
            {
                BanPlayer(playerToBan);
                SendPlayerChatMessage(sender, $"Banned {playerToBan.FisherName}");
                SendGlobalChatMessage($"{playerToBan.FisherName} has been banned from the server.");
            }
        }

        /// <summary>
        /// Handles the "!setjoinable" command.
        /// </summary>
        /// <param name="sender">The player who issued the command.</param>
        /// <param name="commandParts">The command and its arguments.</param>
        private void HandleSetJoinableCommand(WFPlayer sender, string[] commandParts)
        {
            if (!IsPlayerAdmin(sender)) return;

            if (commandParts.Length < 2)
            {
                SendPlayerChatMessage(sender, "Usage: !setjoinable <true/false>");
                return;
            }

            string arg = commandParts[1].ToLower();
            if (arg == "true")
            {
                Server.GameLobby.SetJoinable(true);
                SendPlayerChatMessage(sender, "Opened lobby!");
                if (!Server.CodeOnly)
                {
                    Server.GameLobby.SetData("type", "public");
                    SendPlayerChatMessage(sender, "Unhid server from server list");
                }
            }
            else if (arg == "false")
            {
                Server.GameLobby.SetJoinable(false);
                SendPlayerChatMessage(sender, "Closed lobby!");
                if (!Server.CodeOnly)
                {
                    Server.GameLobby.SetData("type", "code_only");
                    SendPlayerChatMessage(sender, "Hid server from server list");
                }
            }
            else
            {
                SendPlayerChatMessage(sender, $"\"{arg}\" is not true or false!");
            }
        }

        /// <summary>
        /// Handles the "!refreshadmins" command.
        /// </summary>
        /// <param name="sender">The player who issued the command.</param>
        private void HandleRefreshAdminsCommand(WFPlayer sender)
        {
            if (!IsPlayerAdmin(sender)) return;

            Server.ReadAdmins();
            SendPlayerChatMessage(sender, "Admins list refreshed.");
        }
    }
}
