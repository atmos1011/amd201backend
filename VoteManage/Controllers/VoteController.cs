using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VoteManage.Hubs;
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
        private readonly IHubContext<PollHub> _hubContext;
        private readonly ILogger<VoteController> _logger;

        public VoteController(
            IVoteRepo r,
            PollService pollService,
            IHubContext<PollHub> hubContext,
            ILogger<VoteController> logger)
        {
            _repository = r;
            _pollService = pollService;
            _hubContext = hubContext;
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
                return Conflict("This poll is closed.");
            }

            if (!poll.Options.Any(o => o.Index == createVote.OptionIndex))
            {
                return BadRequest("That option does not exist on this poll.");
            }

            // A first time voter has no token yet, so we make one and send it back.
            var voterToken = Request.Headers[VoterTokenHeader].ToString();
            if (string.IsNullOrWhiteSpace(voterToken))
            {
                voterToken = Guid.NewGuid().ToString("N");
            }

            if (await _repository.HasVotedAsync(code, voterToken))
            {
                return Conflict("You have already voted in this poll.");
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
                return Conflict("You have already voted in this poll.");
            }

            _logger.LogInformation("vote saved for poll {Code}, option {OptionIndex}", code, createVote.OptionIndex);

            await _pollService.NotifyVotesRecordedAsync(code);

            var results = await BuildResultsAsync(poll);

            // Push the new numbers to everyone watching this poll's results page.
            await _hubContext.Clients.Group(PollHub.GroupFor(code)).SendAsync("ResultsUpdated", results);

            Response.Headers[VoterTokenHeader] = voterToken;
            return Ok(new VoteResultDto { VoterToken = voterToken, Results = results });
        }

        [HttpGet("{code}/results")]
        public async Task<ActionResult<PollResultDto>> GetResults(string code)
        {
            var poll = await _pollService.GetPollByCodeAsync(code);
            if (poll == null)
            {
                return NotFound();
            }

            return Ok(await BuildResultsAsync(poll));
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

        // Adds up the votes we own and puts the option text from PollManage next to them.
        private async Task<PollResultDto> BuildResultsAsync(PollDetailsDto poll)
        {
            var votes = await _repository.GetByPollCodeAsync(poll.Code);
            var total = votes.Count();

            var results = new PollResultDto
            {
                Code = poll.Code,
                Question = poll.Question,
                Status = poll.Status,
                TotalVotes = total
            };

            foreach (var option in poll.Options.OrderBy(o => o.Index))
            {
                var count = votes.Count(v => v.OptionIndex == option.Index);

                results.Options.Add(new OptionResultDto
                {
                    Index = option.Index,
                    Text = option.Text,
                    Votes = count,
                    // Every option is listed even with 0 votes, so the chart keeps its shape.
                    Percentage = total == 0 ? 0 : Math.Round(count * 100.0 / total, 1)
                });
            }

            return results;
        }
    }
}
