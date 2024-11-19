using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cove.Server.HostedServices
{
    /// <summary>
    /// A hosted service responsible for managing the spawning of metal instances.
    /// </summary>
    public class HostSpawnMetalService : IHostedService, IDisposable
    {
        private readonly ILogger<HostSpawnMetalService> _logger;
        private readonly CoveServer _server;
        private Timer? _timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostSpawnMetalService"/> class.
        /// </summary>
        /// <param name="logger">The logger used for logging service operations.</param>
        /// <param name="server">The server instance that owns this service.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> or <paramref name="server"/> is null.</exception>
        public HostSpawnMetalService(ILogger<HostSpawnMetalService> logger, CoveServer server)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        /// <summary>
        /// Starts the <see cref="HostSpawnMetalService"/> and initializes the periodic timer.
        /// </summary>
        /// <param name="cancellationToken">A token that signals when the service should stop.</param>
        /// <returns>A completed <see cref="Task"/> indicating the service has started.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HostSpawnMetalService is starting.");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(8));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Periodically invoked by the timer to manage metal spawning.
        /// </summary>
        /// <param name="state">Optional state parameter, unused in this implementation.</param>
        private void DoWork(object? state)
        {
            _logger.LogInformation("HostSpawnMetalService is working.");

            try
            {
                UpdateServerBrowserValue();
                ManageMetalSpawning();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during HostSpawnMetalService execution.");
            }
        }

        /// <summary>
        /// Updates the server browser value in the game lobby.
        /// </summary>
        private void UpdateServerBrowserValue()
        {
            try
            {
                _server.GameLobby.SetData("server_browser_value", "0");
                _logger.LogDebug("Updated server browser value.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to update server browser value: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Manages the spawning of metal instances based on server settings and current instance count.
        /// </summary>
        private void ManageMetalSpawning()
        {
            try
            {
                var metalCount = _server.ServerOwnedInstances.Count(a => a.Type == "metal_spawn");
                _logger.LogDebug("Current metal count: {Count}", metalCount);

                if (metalCount > 7)
                {
                    _logger.LogInformation("Metal spawn threshold reached. Skipping spawn.");
                    return;
                }

                if (_server.ShouldSpawnMetal)
                {
                    _server.SpawnMetal();
                    _logger.LogInformation("Spawned a metal instance.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to spawn metal instance.");
            }
        }

        /// <summary>
        /// Stops the <see cref="HostSpawnMetalService"/> and cancels the periodic timer.
        /// </summary>
        /// <param name="cancellationToken">A token that signals when the service should stop.</param>
        /// <returns>A completed <see cref="Task"/> indicating the service has stopped.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HostSpawnMetalService is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes of the resources used by the <see cref="HostSpawnMetalService"/>, including the timer.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
