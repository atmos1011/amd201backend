using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Polls;
using PollBuilder.Contracts.Realtime;

namespace PollBuilder.Polls.Services
{
    /// <summary>
    /// Posts close notifications to RealtimeService. This call goes direct rather than through the
    /// gateway: it is internal traffic, and the gateway deliberately publishes no /internal routes.
    /// </summary>
    public class RealtimeNotifierClient : IRealtimeNotifier
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

        public async Task PollClosedAsync(
            string code, DateTimeOffset? closedAt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_endpoints.RealtimeBaseUrl))
            {
                _logger.LogWarning("ServiceEndpoints__RealtimeBaseUrl is not set, so poll {Code} closed silently", code);
                return;
            }

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, new Uri("internal/poll-closed", UriKind.Relative))
                {
                    Content = JsonContent.Create(
                        new PollClosedNotification(code, PollStatus.Closed, closedAt), options: SerializerOptions)
                };
                request.Headers.Add(ServiceDefaults.InternalApiKeyHeader, _endpoints.InternalApiKey);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "RealtimeService rejected the close notification for {Code}: {StatusCode}",
                        code, response.StatusCode);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                // The poll really is closed in the database. Losing the push only means watchers find
                // out on their next request, so this must not fail the creator's close call.
                _logger.LogError(ex, "Failed to notify RealtimeService that poll {Code} closed", code);
            }
        }
    }
}
