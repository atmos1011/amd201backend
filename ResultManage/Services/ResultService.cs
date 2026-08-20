using ResultManage.Models;

namespace ResultManage.Services
{
    // Service C. It owns no database of its own: it asks PollManage for the question and
    // option text, asks VoteManage for the counts, and puts the two together.
    // Same shape as EnrollmentService in the lab, which uses StudentService and TeacherService.
    public class ResultService
    {
        private readonly PollService _pollService;
        private readonly VoteService _voteService;
        private readonly ILogger<ResultService> _logger;

        public ResultService(
            PollService pollService,
            VoteService voteService,
            ILogger<ResultService> logger)
        {
            _pollService = pollService;
            _voteService = voteService;
            _logger = logger;
        }

        public async Task<PollResultDto?> GetResultsAsync(string code)
        {
            var poll = await _pollService.GetPollByCodeAsync(code);
            if (poll == null)
            {
                _logger.LogWarning("cannot build results, poll {Code} was not found", code);
                return null;
            }

            var counts = await _voteService.GetCountsAsync(code) ?? new VoteCountsDto();

            var results = new PollResultDto
            {
                Code = poll.Code,
                Question = poll.Question,
                Status = poll.Status,
                TotalVotes = counts.TotalVotes
            };

            foreach (var option in poll.Options.OrderBy(o => o.Index))
            {
                var votes = counts.Counts.FirstOrDefault(c => c.OptionIndex == option.Index)?.Votes ?? 0;

                results.Options.Add(new OptionResultDto
                {
                    Index = option.Index,
                    Text = option.Text,
                    Votes = votes,
                    // Every option is listed even with 0 votes, so the chart keeps its shape.
                    Percentage = counts.TotalVotes == 0
                        ? 0
                        : Math.Round(votes * 100.0 / counts.TotalVotes, 1)
                });
            }

            return results;
        }
    }
}
