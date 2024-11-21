namespace Cove.Server.Actor
{
    /// <summary>
    /// Represents a player in the game, inheriting from <see cref="WFActor"/>.
    /// </summary>
    public class WFPlayer : WFActor
    {
        public SteamId SteamId { get; set; }
        public string FisherId { get; set; }
        public string FisherName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WFPlayer"/> class.
        /// </summary>
        /// <param name="steamId">The Steam Id of the player.</param>
        /// <param name="fisherName">The display name of the player.</param>
        public WFPlayer(SteamId steamId, string fisherName)
            : base(0, "player", Vector3.Zero)
        {
            SteamId = steamId;
            FisherName = fisherName ?? throw new ArgumentNullException(nameof(fisherName));

            // Generate a random FisherId consisting of 3 alphanumeric characters
            FisherId = GenerateRandomFisherId(3);

            Position = Vector3.Zero;
            ShouldDespawn = false;
        }

        /// <summary>
        /// Generates a random Fisher Id of the specified length.
        /// </summary>
        /// <param name="length">The length of the Fisher Id to generate.</param>
        /// <returns>A random alphanumeric string.</returns>
        private static string GenerateRandomFisherId(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();

            return new string([.. Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)])]);
        }
    }
}
