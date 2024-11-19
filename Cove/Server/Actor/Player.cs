namespace Cove.Server.Actor
{
  /// <summary>
  /// Represents a player in the game, inheriting from <see cref="WFActor"/>.
  /// </summary>
  public class WFPlayer : WFActor
  {
    /// <summary>
    /// Gets or sets the Steam ID of the player.
    /// </summary>
    public SteamId SteamId { get; set; }

    /// <summary>
    /// Gets or sets the unique Fisher ID of the player.
    /// </summary>
    public string FisherID { get; set; }

    /// <summary>
    /// Gets or sets the display name of the player.
    /// </summary>
    public string FisherName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WFPlayer"/> class.
    /// </summary>
    /// <param name="steamId">The Steam ID of the player.</param>
    /// <param name="fisherName">The display name of the player.</param>
    public WFPlayer(SteamId steamId, string fisherName)
        : base(0, "player", Vector3.Zero)
    {
      SteamId = steamId;
      FisherName = fisherName ?? throw new ArgumentNullException(nameof(fisherName));

      // Generate a random FisherID consisting of 3 alphanumeric characters
      FisherID = GenerateRandomFisherID(3);

      Position = Vector3.Zero;
      ShouldDespawn = false;
    }

    /// <summary>
    /// Generates a random Fisher ID of the specified length.
    /// </summary>
    /// <param name="length">The length of the Fisher ID to generate.</param>
    /// <returns>A random alphanumeric string.</returns>
    private static string GenerateRandomFisherID(int length)
    {
      const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
      var random = new Random();

      return new string(Enumerable.Range(0, length)
          .Select(_ => chars[random.Next(chars.Length)])
          .ToArray());
    }
  }
}
