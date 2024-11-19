using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cove.Server.HostedServices
{
  /// <summary>
  /// A hosted service responsible for periodically spawning and managing server-owned instances.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="HostSpawnService"/> class.
  /// </remarks>
  /// <param name="logger">The logger used for logging service operations.</param>
  /// <param name="server">The server instance that owns this service.</param>
  /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> or <paramref name="server"/> is null.</exception>
  public class HostSpawnService(ILogger<HostSpawnService> logger, CoveServer server) : IHostedService, IDisposable
    {
        private readonly ILogger<HostSpawnService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly CoveServer _server = server ?? throw new ArgumentNullException(nameof(server));
        private Timer? _timer;
        private float _rainChance = 0f;

    /// <summary>
    /// Starts the <see cref="HostSpawnService"/> and initializes the periodic timer for spawning instances.
    /// </summary>
    /// <param name="cancellationToken">A token that signals when the service should stop.</param>
    /// <returns>A completed <see cref="Task"/> indicating the service has started.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HostSpawnService is starting.");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Periodically invoked by the timer to spawn and manage server-owned instances.
        /// </summary>
        /// <param name="state">Optional state parameter, unused in this implementation.</param>
        private void DoWork(object? state)
        {
            _logger.LogInformation("HostSpawnService is working.");

            try
            {
                RemoveExpiredInstances();

                var type = DetermineSpawnType();
                SpawnType(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during HostSpawnService work.");
            }
        }

        /// <summary>
        /// Removes expired instances from the server.
        /// </summary>
        private void RemoveExpiredInstances()
        {
            try
            {
                var expiredInstances = _server.ServerOwnedInstances
                    .Where(inst => inst.ShouldDespawn && (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - inst.SpawnTime.ToUnixTimeSeconds()) > inst.DespawnTime)
                    .ToList();

                foreach (var inst in expiredInstances)
                {
                    _server.ServerOwnedInstances.Remove(inst);
                    _logger.LogInformation("Removed {Type}, decayed.", inst.Type);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDebug("Error during instance removal: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Determines the type of instance to spawn based on random factors and server settings.
        /// </summary>
        /// <returns>The type of instance to spawn.</returns>
        private string DetermineSpawnType()
        {
            var random = new Random();
            var beginningTypes = new[] { "fish", "none" };
            var type = beginningTypes[random.Next(beginningTypes.Length)];

            // Meteor spawn logic
            if (random.NextDouble() < 0.01 && random.NextDouble() < 0.4 && _server.ShouldSpawnMeteor)
            {
                type = "meteor";
            }

            // Rain spawn logic
            if (random.NextDouble() < _rainChance && random.NextDouble() < 0.12)
            {
                type = "rain";
                _rainChance = 0; // Reset rain chance after spawn
            }
            else
            {
                if (random.NextDouble() < 0.75)
                {
                    _rainChance += 0.001f * _server.RainMultiplier;
                }
            }

            // Void portal spawn logic
            if (random.NextDouble() < 0.01 && random.NextDouble() < 0.25 && _server.ShouldSpawnPortal)
            {
                type = "void_portal";
            }

            return type;
        }

        /// <summary>
        /// Spawns an instance of the specified type.
        /// </summary>
        /// <param name="type">The type of instance to spawn.</param>
        private void SpawnType(string type)
        {
            switch (type)
            {
                case "none":
                    break;

                case "fish":
                    // Prevent excessive fish spawning to avoid lag
                    if (_server.ServerOwnedInstances.Count > 15) return;
                    _server.SpawnFish();
                    _logger.LogInformation("Spawned a fish.");
                    break;

                case "meteor":
                    _server.SpawnFish("fish_spawn_alien");
                    _logger.LogInformation("Spawned a meteor.");
                    break;

                case "rain":
                    _server.SpawnRainCloud();
                    _logger.LogInformation("Spawned rain.");
                    break;

                case "void_portal":
                    _server.SpawnVoidPortal();
                    _logger.LogInformation("Spawned a void portal.");
                    break;

                default:
                    _logger.LogWarning("Unknown spawn type: {Type}", type);
                    break;
            }
        }

        /// <summary>
        /// Stops the <see cref="HostSpawnService"/> and cancels the periodic timer.
        /// </summary>
        /// <param name="cancellationToken">A token that signals when the service should stop.</param>
        /// <returns>A completed <see cref="Task"/> indicating the service has stopped.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HostSpawnService is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes of the resources used by the <see cref="HostSpawnService"/>, including the timer.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
