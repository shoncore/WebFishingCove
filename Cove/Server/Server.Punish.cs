namespace Cove.Server
{
    public partial class CoveServer
    {
        private const string BansFileName = "bans.txt";

        /// <summary>
        /// Bans a player from the server and optionally saves the ban to a file.
        /// </summary>
        /// <param name="id">The SteamId of the player to ban.</param>
        /// <param name="saveToFile">Whether to save the ban to the bans file.</param>
        public void BanPlayer(SteamId id, bool saveToFile = false)
        {
            var banPacket = new Dictionary<string, object>
            {
                { "type", "ban" }
            };

            SendPacketToPlayer(banPacket, id);

            if (saveToFile)
            {
                WriteToBansFile(id);
            }

            SendBlacklistPacketToAll(id.Value.ToString());
        }

        /// <summary>
        /// Checks if a player is banned based on their SteamId.
        /// </summary>
        /// <param name="id">The SteamId of the player to check.</param>
        /// <returns>True if the player is banned; otherwise, false.</returns>
        public bool IsPlayerBanned(SteamId id)
        {
            string filePath = GetBansFilePath();

            if (!File.Exists(filePath))
            {
                return false;
            }

            var fileContent = File.ReadLines(filePath);
            return fileContent.Any(line => line.Contains(id.Value.ToString()));
        }

        /// <summary>
        /// Writes a player's SteamId and name to the bans file.
        /// </summary>
        /// <param name="id">The SteamId of the player to write.</param>
        private void WriteToBansFile(SteamId id)
        {
            string filePath = GetBansFilePath();

            var player = AllPlayers.Find(p => p.SteamId == id);
            if (player == null)
            {
                Logger.LogWarning("Cannot find player with SteamId {SteamId} to write to bans file.", id.Value);
                return;
            }

            try
            {
                File.AppendAllText(filePath, $"\n{id.Value} #{player.FisherName}");
                Logger.LogInformation("Added {FisherName} [{SteamId}] to bans file.", player.FisherName, player.SteamId);
            }
            catch (IOException ex)
            {
                Logger.LogError(ex, "Failed to write to bans file");
            }
        }

        /// <summary>
        /// Kicks a player from the server.
        /// </summary>
        /// <param name="id">The SteamId of the player to kick.</param>
        public static void KickPlayer(SteamId id)
        {
            var kickPacket = new Dictionary<string, object>
            {
                { "type", "kick" }
            };

            SendPacketToPlayer(kickPacket, id);
        }

        /// <summary>
        /// Retrieves the full file path to the bans file.
        /// </summary>
        /// <returns>The bans file path.</returns>
        private static string GetBansFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, BansFileName);
        }
    }
}
