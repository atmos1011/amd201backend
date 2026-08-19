using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Realtime;
using PollBuilder.Contracts.Voting;
using PollBuilder.Voting.Repo;
using PollBuilder.Voting.Services;

namespace PollBuilder.Voting.Services
{
    /// <summary>
    /// Hands new tallies to RealtimeService, which owns the SignalR hub and the connected browsers.
    /// This call goes direct rather than through the gateway: it is an internal call, not public API.
    /// </summary>
    public class RealtimeNotifierClient : IPollNotifier
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _httpClient;
        private readonly ServiceEndpointOptions _endpoints;
        private readonly ILogger<RealtimeNotifierClient> _logger;

        public RealtimeNotifierClient(
            HttpClient httpClient,
            IOptions<ServiceEndpointOptions> endpoints,
            ILogger<RealtimeNotifierClient> logger)
        {
            _httpClient = httpClient; // BaseAddress is set in Program.cs
            _endpoints = endpoints.Value;
            _logger = logger;
        }

        public async Task ResultsUpdatedAsync(PollResultsResponse results, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(results);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("internal/broadcast", UriKind.Relative))
                {
                    Content = JsonContent.Create(new BroadcastRequest(results), options: SerializerOptions)
                };
                request.Headers.Add(ServiceDefaults.InternalApiKeyHeader, _endpoints.InternalApiKey);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "RealtimeService rejected broadcast for {Code}: {StatusCode}", results.Code, response.StatusCode);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                // A broadcast failure must never turn a successfully recorded vote into an HTTP error;
                // the voter still gets results in the response body, and watchers refresh on reconnect.
                _logger.LogError(ex, "Failed to broadcast results for poll {Code}", results.Code);
            }
        }
    }
}
