using VoteManage.Models;

namespace VoteManage.Services
{
    // Talks to ResultManage (service C) through the gateway. After a vote is saved we ask
    // C to rebuild the results and push them to everyone watching, and C hands the finished
    // results back so we can return them to the voter.
    public class ResultService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ResultService> _logger;

        public ResultService(HttpClient httpClient, ILogger<ResultService> logger)
        {
            _httpClient = httpClient; // BaseAddress is set in Program.cs
            _logger = logger;
        }

        public async Task<PollResultDto?> BroadcastAsync(string code)
        {
            var requestPath = $"api/polls/{code}/results/broadcast"; // path on the gateway

            try
            {
                var response = await _httpClient.PostAsync(requestPath, null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PollResultDto>();
                }

                _logger.LogWarning("ResultManage rejected the broadcast for {Code}. Status: {StatusCode}",
                    code, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                // The vote is already saved, so a problem here must not fail the request.
                _logger.LogError(ex, "Could not ask ResultManage to push new results for {Code}.", code);
                return null;
            }
        }
    }
}
