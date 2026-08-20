using ResultManage.Models;

namespace ResultManage.Services
{
    // Talks to PollManage (service A) through the gateway, the same way StudentService
    // does in the microservices lab.
    public class PollService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PollService> _logger;

        public PollService(HttpClient httpClient, ILogger<PollService> logger)
        {
            _httpClient = httpClient; // BaseAddress is set in Program.cs
            _logger = logger;
        }

        public async Task<PollDetailsDto?> GetPollByCodeAsync(string code)
        {
            var requestPath = $"api/polls/{code}"; // path on the gateway
            _logger.LogInformation("Fetching poll from: {BaseAddress}{Path}", _httpClient.BaseAddress, requestPath);

            try
            {
                var response = await _httpClient.GetAsync(requestPath);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PollDetailsDto>();
                }

                _logger.LogWarning("Failed to get poll {Code}. Status: {StatusCode}", code, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while fetching poll {Code}.", code);
                return null;
            }
        }
    }
}
