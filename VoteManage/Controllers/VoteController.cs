using Microsoft.AspNetCore.Mvc;
using VoteManage.Models;
using VoteManage.Repo;
using VoteManage.Services;

namespace VoteManage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VoteController : ControllerBase
    {
        // The header the browser sends so we know it is the same voter as last time
        public const string VoterTokenHeader = "X-Voter-Token";

        private readonly IVoteRepo _repository;
        private readonly PollService _pollService;
        private readonly ResultService _resultService;
        private readonly ILogger<VoteController> _logger;

        public VoteController(
            IVoteRepo r,
            PollService pollService,
            ResultService resultService,
            ILogger<VoteController> logger)
        {
            _repository = r;
            _pollService = pollService;
            _resultService = resultService;
            _logger = logger;
        }

        [HttpPost("{code}")]
        public async Task<ActionResult<VoteResultDto>> Create(string code, CreateVote createVote)
        {
            // Ask PollManage about the poll. We do not have the Polls table here.
            var poll = await _pollService.GetPollByCodeAsync(code);
            if (poll == null)
            {
                _logger.LogWarning("poll with code {Code} not found", code);
                return NotFound();
            }

            if (poll.Status != "Open")
            {
                return Conflict(new { error = "poll_closed", message = "This poll is closed." });
            }

            if (!poll.Options.Any(o => o.Index == createVote.OptionIndex))
            {
                return BadRequest(new { error = "invalid_option", message = "That option does not exist on this poll." });
            }

            // A first time voter has no token yet, so we make one and send it back.
            var voterToken = Request.Headers[VoterTokenHeader].ToString();
            if (string.IsNullOrWhiteSpace(voterToken))
            {
                voterToken = Guid.NewGuid().ToString("N");
            }

            if (await _repository.HasVotedAsync(code, voterToken))
            {
                return Conflict(new { error = "already_voted", message = "You have already voted in this poll." });
            }

            var saved = await _repository.AddAsync(new Vote
            {
                PollCode = code,
                OptionIndex = createVote.OptionIndex,
                VoterToken = voterToken,
                VotedAt = DateTime.UtcNow
            });

            if (saved == null)
            {
                // The unique index rejected it, so two votes were sent at the same time.
                return Conflict(new { error = "already_voted", message = "You have already voted in this poll." });
            }

            _logger.LogInformation("vote saved for poll {Code}, option {OptionIndex}", code, createVote.OptionIndex);

            await _pollService.NotifyVotesRecordedAsync(code);

            // Ask ResultManage to rebuild the results and push them over SignalR. It hands
            // the finished results back, so the voter gets them without another request.
            var results = await _resultService.BroadcastAsync(code);

            Response.Headers[VoterTokenHeader] = voterToken;
            return Ok(new VoteResultDto { VoterToken = voterToken, Results = results });
        }

        // The raw counts this service owns. ResultManage calls this and adds the option
        // text from PollManage to turn it into the results the chart shows.
        [HttpGet("{code}/counts")]
        public async Task<ActionResult<VoteCountsDto>> GetCounts(string code)
        {
            var votes = await _repository.GetByPollCodeAsync(code);

            return Ok(new VoteCountsDto
            {
                TotalVotes = votes.Count(),
                Counts = votes
                    .GroupBy(v => v.OptionIndex)
                    .Select(g => new OptionCountDto { OptionIndex = g.Key, Votes = g.Count() })
                    .ToList()
            });
        }

        // Lets the page know whether this browser has already voted.
        [HttpGet("{code}/me")]
        public async Task<ActionResult> GetMe(string code)
        {
            var voterToken = Request.Headers[VoterTokenHeader].ToString();
            var hasVoted = !string.IsNullOrWhiteSpace(voterToken)
                && await _repository.HasVotedAsync(code, voterToken);

            return Ok(new { hasVoted });
        }
    }
}
