namespace Cove.Server
{
    public partial class CoveServer
    {
        private const string BanFileName = "banned_players.txt";

        /// <summary>
        /// Bans a player from the server and optionally saves the ban to a file.
        /// </summary>
        /// <param name="steamId">The SteamId of the player to ban.</param>
        /// <param name="saveToFile">Whether to save the ban to the bans file.</param>
        public void BanPlayer(SteamId steamId, bool saveToFile = false)
        {
            Logger.LogInformation("Banning player {SteamId}", steamId.Value);

            var banPacket = new Dictionary<string, object>
            {
                { "type", "ban" }
            };

            SendPacketToPlayer(banPacket, steamId);

            if (saveToFile)
            {
                WriteToBansFile(steamId.Value);
            }

            SendBlacklistPacketToAll(steamId.Value.ToString());
        }

        /// <summary>
        /// Checks if a player is banned based on their SteamId.
        /// </summary>
        /// <param name="steamId">The SteamId of the player to check.</param>
        /// <returns>True if the player is banned; otherwise, false.</returns>
        public bool IsPlayerBanned(SteamId steamId)
        {
            string filePath = GetBansFilePath();

            if (!File.Exists(filePath))
            {
                return false;
            }

            return File.ReadLines(filePath)
                .Select(line => line.Split('#').FirstOrDefault()?.Trim())
                .Any(bannedSteamId => ulong.TryParse(bannedSteamId, out var parsedId) && parsedId == steamId.Value);
        }


        /// <summary>
        /// Writes a player's SteamId and name to the bans file.
        /// </summary>
        /// <param name="steamId">The SteamId of the player to write.</param>
        private void WriteToBansFile(SteamId steamId)
        {
            string filePath = GetBansFilePath();

            var player = AllPlayers.Find(p => p.SteamId.Value == steamId.Value);
            if (player == null)
            {
                Logger.LogWarning("Cannot find player with SteamId {SteamId} to write to bans file.", steamId.Value);
                return;
            }

            try
            {
                File.AppendAllText(filePath, $"\n{steamId.Value} #{player.FisherName}");
                Logger.LogInformation("Added {FisherName} [{SteamId}] to bans file.", player.FisherName, player.SteamId.Value);
            }
            catch (IOException ex)
            {
                Logger.LogError(ex, "Failed to write to bans file");
            }
        }

        /// <summary>
        /// Kicks a player from the server.
        /// </summary>
        /// <param name="steamId">The SteamId of the player to kick.</param>
        public static void KickPlayer(SteamId steamId)
        {
            var kickPacket = new Dictionary<string, object>
            {
                { "type", "kick" }
            };

            SendPacketToPlayer(kickPacket, steamId);
        }

        /// <summary>
        /// Retrieves the full file path to the bans file.
        /// </summary>
        /// <returns>The bans file path.</returns>
        private static string GetBansFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, BanFileName);
        }
    }
}
