namespace Cove.Server.HostedServices
{
    /// <summary>
    /// Represents the request body sent to the HLS server list endpoint.
    /// </summary>
    class RequestBody
    {
        public string Host { get; set; } = string.Empty;
        public string LobbyCode { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string LobbyType { get; set; } = string.Empty;
        public int PlayerCap { get; set; }
        public bool AgeRestricted { get; set; }
        public string Map { get; set; } = "default"; // Placeholder, make configurable in the future
        public string Title { get; set; } = string.Empty;
        public string[] Mods { get; set; } = Array.Empty<string>(); // Placeholder, make configurable in the future
        public string Country { get; set; } = string.Empty;
        public int CurrentPlayers { get; set; }
    }

    /// <summary>
    /// A hosted service responsible for sending periodic heartbeats to the HLS server list endpoint.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="HLSServerListService"/> class.
    /// </remarks>
    /// <param name="logger">The logger used for logging service operations.</param>
    /// <param name="server">The server instance that owns this service.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> or <paramref name="server"/> is null.</exception>
    public class HLSServerListService(ILogger<HLSServerListService> logger, CoveServer server)
        : IHostedService,
            IDisposable
    {
        private readonly ILogger<HLSServerListService> _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly CoveServer _server =
            server ?? throw new ArgumentNullException(nameof(server));
        private Timer? _timer;
        private const string Endpoint = "https://hooklinesinker.lol/servers";
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = false };

        /// <summary>
        /// Starts the <see cref="HLSServerListService"/> and initializes the periodic timer.
        /// </summary>
        /// <param name="cancellationToken">A token that signals when the service should stop.</param>
        /// <returns>A completed <see cref="Task"/> indicating the service has started.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HLSServerListService is starting.");
            _timer = new Timer(
                DoWorkAsync,
                null,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(45))
            );
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends periodic heartbeats to the HLS server list endpoint.
        /// </summary>
        /// <param name="state">Optional state parameter, unused in this implementation.</param>
        private async void DoWorkAsync(object? state)
        {
            try
            {
                var requestBody = CreateRequestBody();
                var jsonBody = JsonSerializer.Serialize(
                    requestBody,
                    JsonSerializerOptions
                );

                using var client = new HttpClient();
                using var content = new StringContent(
                    jsonBody,
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync(Endpoint, content);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Heartbeat sent to HLS server list successfully.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Failed to send heartbeat to HLS server list. Status Code: {StatusCode}",
                        response.StatusCode
                    );
                    _logger.LogError("Response: {ErrorContent}", errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while sending a heartbeat to the HLS server list."
                );
            }
        }

        /// <summary>
        /// Creates the request body for the HLS server list heartbeat.
        /// </summary>
        /// <returns>A populated <see cref="RequestBody"/> instance.</returns>
        private RequestBody CreateRequestBody()
        {
            return new RequestBody
            {
                Host = Steamworks.SteamClient.Name,
                LobbyCode = _server.LobbyCode,
                Version = _server.WebFishingGameVersion,
                LobbyType = _server.CodeOnly ? "Code Only" : "Public",
                PlayerCap = _server.MaxPlayers,
                AgeRestricted = _server.AgeRestricted,
                Map = "default", // Placeholder, make configurable in the future
                Title = _server.ServerName,
                Mods = Array.Empty<string>(), // Placeholder, make configurable in the future
                Country = Steamworks.SteamUtils.IpCountry,
                CurrentPlayers = _server.AllPlayers.Count
            };
        }

        /// <summary>
        /// Stops the <see cref="HLSServerListService"/> and cancels the periodic timer.
        /// </summary>
        /// <param name="cancellationToken">A token that signals when the service should stop.</param>
        /// <returns>A completed <see cref="Task"/> indicating the service has stopped.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HLSServerListService is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes of the resources used by the <see cref="HLSServerListService"/>, including the timer.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
