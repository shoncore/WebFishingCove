using Cove.GodotFormat;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cove.Server.HostedServices
{
    /// <summary>
    /// A hosted service responsible for managing and updating server-owned actors periodically.
    /// </summary>
    public class ActorUpdateService : IHostedService, IDisposable
    {
        private readonly ILogger<ActorUpdateService> _logger;
        private readonly CoveServer _server;
        private Timer? _timer;
        private int _updateCounter = 0;
        private readonly Dictionary<long, Vector3> _pastTransforms = new();
        private const int IdleUpdateThreshold = 30;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorUpdateService"/> class.
        /// </summary>
        /// <param name="logger">The logger used for logging service operations.</param>
        /// <param name="server">The server instance that owns this service.</param>
        /// <exception cref="ArgumentNullException">Thrown when either <paramref name="logger"/> or <paramref name="server"/> is null.</exception>
        public ActorUpdateService(ILogger<ActorUpdateService> logger, CoveServer server)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        /// <summary>
        /// Starts the <see cref="ActorUpdateService"/> and initializes the periodic timer for actor updates.
        /// </summary>
        /// <param name="cancellationToken">A token that signals when the service should stop.</param>
        /// <returns>A completed <see cref="Task"/> indicating the service has started.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ActorUpdateService is starting.");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / 12));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Periodically invoked by the timer to update actors and synchronize their state with connected clients.
        /// </summary>
        /// <param name="state">Optional state parameter, unused in this implementation.</param>
        private void DoWork(object? state)
        {
            _updateCounter++;

            // Trigger updates for all loaded plugins.
            foreach (var plugin in _server.LoadedPlugins)
            {
                plugin.plugin.OnUpdate();
            }

            try
            {
                // Iterate through all server-owned instances and update their state.
                foreach (var actor in _server.GetServerOwnedInstances())
                {
                    actor.OnUpdate();

                    if (!_pastTransforms.ContainsKey(actor.InstanceID))
                    {
                        _pastTransforms[actor.InstanceID] = Vector3.Zero;
                    }

                    // Send updates to clients if the actor has moved or at the idle update threshold.
                    if (actor.Position != _pastTransforms[actor.InstanceID] || _updateCounter == IdleUpdateThreshold)
                    {
                        var packet = new Dictionary<string, object>
                        {
                            { "type", "actor_update" },
                            { "actor_id", actor.InstanceID },
                            { "pos", actor.Position },
                            { "rot", actor.Rotation }
                        };

                        _pastTransforms[actor.InstanceID] = actor.Position;
                        _server.SendPacketToPlayers(packet);
                    }
                }
            }
            catch (InvalidOperationException e)
            {
                // Log any iteration errors, usually caused by list modifications during iteration.
                _logger.LogDebug("Actor list was modified during iteration: {Message}", e.Message);
            }

            if (_updateCounter >= IdleUpdateThreshold)
            {
                _updateCounter = 0;
            }
        }

        /// <summary>
        /// Stops the <see cref="ActorUpdateService"/> and cancels the periodic timer.
        /// </summary>
        /// <param name="cancellationToken">A token that signals when the service should stop.</param>
        /// <returns>A completed <see cref="Task"/> indicating the service has stopped.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ActorUpdateService is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes of the resources used by the <see cref="ActorUpdateService"/>, including the timer.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
