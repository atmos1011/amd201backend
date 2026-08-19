using VoteManage.Models;

namespace VoteManage.Services
{
    // Talks to PollManage. Same idea as StudentService in the microservices lab: the request
    // goes through the API gateway, so there is only one base URL to change.
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

        // Tells PollManage that this poll now has votes, so the creator can no longer edit it.
        public async Task NotifyVotesRecordedAsync(string code)
        {
            try
            {
                await _httpClient.PostAsync($"api/polls/{code}/votes-recorded", null);
            }
            catch (Exception ex)
            {
                // The vote is already saved, so this must not fail the request.
                _logger.LogError(ex, "Could not tell PollManage that poll {Code} has votes.", code);
            }
        }
    }
}
