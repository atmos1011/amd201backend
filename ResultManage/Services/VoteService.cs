using ResultManage.Models;

namespace ResultManage.Services
{
    // Talks to VoteManage (service B) through the gateway, the same way TeacherService
    // does in the microservices lab.
    public class VoteService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VoteService> _logger;

        public VoteService(HttpClient httpClient, ILogger<VoteService> logger)
        {
            _httpClient = httpClient; // BaseAddress is set in Program.cs
            _logger = logger;
        }

        public async Task<VoteCountsDto?> GetCountsAsync(string code)
        {
            var requestPath = $"api/polls/{code}/vote/counts"; // path on the gateway
            _logger.LogInformation("Fetching counts from: {BaseAddress}{Path}", _httpClient.BaseAddress, requestPath);

            try
            {
                var response = await _httpClient.GetAsync(requestPath);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<VoteCountsDto>();
                }

                _logger.LogWarning("Failed to get counts for {Code}. Status: {StatusCode}", code, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while fetching counts for {Code}.", code);
                return null;
            }
        }
    }
}
