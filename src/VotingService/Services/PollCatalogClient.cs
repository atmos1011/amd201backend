using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PollBuilder.Contracts.Errors;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Polls;
using PollBuilder.Voting.Repo;
using PollBuilder.Voting.Services;

namespace PollBuilder.Voting.Services
{
    /// <summary>
    /// Talks to PollService over HTTP. Requests go through the API gateway, matching the pattern from
    /// the microservices lab: services address each other by the one public URL, so there is a single
    /// place to update when a host changes.
    /// </summary>
    public class PollCatalogClient : IPollCatalog
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _httpClient;
        private readonly ServiceEndpointOptions _endpoints;
        private readonly ILogger<PollCatalogClient> _logger;

        public PollCatalogClient(
            HttpClient httpClient,
            IOptions<ServiceEndpointOptions> endpoints,
            ILogger<PollCatalogClient> logger)
        {
            _httpClient = httpClient; // BaseAddress is set in Program.cs
            _endpoints = endpoints.Value;
            _logger = logger;
        }

        public async Task<PollDto?> GetPollAsync(string code, CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _httpClient
                    .GetAsync(new Uri($"api/polls/{Uri.EscapeDataString(code)}", UriKind.Relative), cancellationToken)
                    ;

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                return await response.Content
                    .ReadFromJsonAsync<PollDto>(SerializerOptions, cancellationToken)
                    ;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                // PollService being down must not look like "poll does not exist" - that would let a
                // vote silently disappear. Surface it as 503 instead.
                _logger.LogError(ex, "PollService unreachable while loading poll {Code}", code);
                throw new UpstreamServiceException("poll");
            }
        }

        public async Task NotifyVotesRecordedAsync(string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_endpoints.PollServiceBaseUrl))
            {
                _logger.LogWarning(
                    "ServiceEndpoints__PollServiceBaseUrl is not set, so poll {Code} will stay editable after voting starts",
                    code);
                return;
            }

            try
            {
                // Direct to PollService, not through the gateway: internal routes are not published there.
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    new Uri(
                        new Uri(_endpoints.PollServiceBaseUrl.TrimEnd('/') + "/"),
                        $"internal/polls/{Uri.EscapeDataString(code)}/votes-recorded"));
                request.Headers.Add(ServiceDefaults.InternalApiKeyHeader, _endpoints.InternalApiKey);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "PollService rejected votes-recorded for {Code}: {StatusCode}", code, response.StatusCode);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                // The vote is already committed. Losing this notification only means the creator could
                // still edit the poll, so it is logged rather than escalated.
                _logger.LogError(ex, "Could not notify PollService that poll {Code} has votes", code);
            }
        }
    }
}
