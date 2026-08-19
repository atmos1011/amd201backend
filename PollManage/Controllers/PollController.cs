using Microsoft.AspNetCore.Mvc;
using PollManage.Models;
using PollManage.Repo;

namespace PollManage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PollController : ControllerBase
    {
        // The header the creator sends to prove the poll is theirs
        public const string CreatorTokenHeader = "X-Creator-Token";

        private readonly IPollRepo _repository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PollController> _logger;

        public PollController(IPollRepo r, IConfiguration configuration, ILogger<PollController> logger)
        {
            _repository = r;
            _configuration = configuration;
            _logger = logger;
        }

        public static PollDto MapToDto(Poll p)
        {
            return new PollDto
            {
                Code = p.Code,
                Question = p.Question,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                ClosedAt = p.ClosedAt,
                HasVotes = p.HasVotes,
                Options = p.Options
                    .OrderBy(o => o.OptionIndex)
                    .Select(o => new PollOptionDto { Index = o.OptionIndex, Text = o.Text })
                    .ToList()
            };
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<PollDto>> Get(string code)
        {
            var poll = await _repository.GetByCodeAsync(code);
            if (poll == null)
            {
                _logger.LogWarning("poll with code {Code} not found", code);
                return NotFound();
            }

            return Ok(MapToDto(poll));
        }

        [HttpPost]
        public async Task<ActionResult<CreatedPollDto>> Create(CreatePoll createPoll)
        {
            if (createPoll.Options.Any(o => string.IsNullOrWhiteSpace(o)))
            {
                return BadRequest("Options cannot be empty.");
            }

            var poll = new Poll
            {
                Code = await GenerateCodeAsync(),
                Question = createPoll.Question.Trim(),
                Status = "Open",
                CreatedAt = DateTime.UtcNow,
                CreatorToken = Guid.NewGuid().ToString("N")
            };

            for (var i = 0; i < createPoll.Options.Count; i++)
            {
                poll.Options.Add(new PollOption { OptionIndex = i, Text = createPoll.Options[i].Trim() });
            }

            var created = await _repository.AddAsync(poll);
            _logger.LogInformation("created poll {Code}", created.Code);

            var dto = MapToDto(created);
            var shareBaseUrl = _configuration["ShareBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";

            return CreatedAtAction(nameof(Get), new { code = created.Code }, new CreatedPollDto
            {
                Code = dto.Code,
                Question = dto.Question,
                Status = dto.Status,
                CreatedAt = dto.CreatedAt,
                ClosedAt = dto.ClosedAt,
                HasVotes = dto.HasVotes,
                Options = dto.Options,
                CreatorToken = created.CreatorToken,
                ShareUrl = $"{shareBaseUrl.TrimEnd('/')}/poll/{created.Code}"
            });
        }

        // Replace the whole poll. Only the creator can do this.
        [HttpPut("{code}")]
        public async Task<ActionResult<PollDto>> Update(string code, UpdatePoll updatePoll)
        {
            var poll = await _repository.GetByCodeAsync(code);
            if (poll == null)
            {
                return NotFound();
            }

            if (!IsCreator(poll))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Wrong or missing creator token.");
            }

            if (poll.HasVotes)
            {
                // Changing the options now would move existing votes onto different text.
                return Conflict("This poll already has votes, so it can no longer be edited.");
            }

            poll.Question = updatePoll.Question.Trim();
            ReplaceOptions(poll, updatePoll.Options);

            await _repository.UpdateAsync(poll);
            return Ok(MapToDto(poll));
        }

        // Change only the fields that are sent. Also how a poll is closed or reopened.
        [HttpPatch("{code}")]
        public async Task<ActionResult<PollDto>> Patch(string code, PatchPoll patchPoll)
        {
            var poll = await _repository.GetByCodeAsync(code);
            if (poll == null)
            {
                return NotFound();
            }

            if (!IsCreator(poll))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Wrong or missing creator token.");
            }

            if ((patchPoll.Question != null || patchPoll.Options != null) && poll.HasVotes)
            {
                return Conflict("This poll already has votes, so it can no longer be edited.");
            }

            if (patchPoll.Question != null)
            {
                poll.Question = patchPoll.Question.Trim();
            }

            if (patchPoll.Options != null)
            {
                ReplaceOptions(poll, patchPoll.Options);
            }

            if (patchPoll.Status != null)
            {
                SetStatus(poll, patchPoll.Status);
            }

            await _repository.UpdateAsync(poll);
            return Ok(MapToDto(poll));
        }

        // Shortcut for PATCH { "status": "Closed" }
        [HttpPost("{code}/close")]
        public async Task<ActionResult<PollDto>> Close(string code)
        {
            var poll = await _repository.GetByCodeAsync(code);
            if (poll == null)
            {
                return NotFound();
            }

            if (!IsCreator(poll))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Wrong or missing creator token.");
            }

            SetStatus(poll, "Closed");
            await _repository.UpdateAsync(poll);
            _logger.LogInformation("poll {Code} was closed", code);

            return Ok(MapToDto(poll));
        }

        // Called by VoteManage after the first vote is saved, so the poll can be locked for editing.
        [HttpPost("{code}/votes-recorded")]
        public async Task<ActionResult> VotesRecorded(string code)
        {
            var poll = await _repository.MarkHasVotesAsync(code);
            if (poll == null)
            {
                return NotFound();
            }

            return NoContent();
        }

        private bool IsCreator(Poll poll)
        {
            var token = Request.Headers[CreatorTokenHeader].ToString();
            return !string.IsNullOrWhiteSpace(token) && token == poll.CreatorToken;
        }

        private static void ReplaceOptions(Poll poll, List<string> options)
        {
            poll.Options.Clear();
            for (var i = 0; i < options.Count; i++)
            {
                poll.Options.Add(new PollOption { OptionIndex = i, Text = options[i].Trim() });
            }
        }

        private static void SetStatus(Poll poll, string status)
        {
            if (status == "Closed")
            {
                poll.Status = "Closed";
                poll.ClosedAt = DateTime.UtcNow;
            }
            else
            {
                poll.Status = "Open";
                poll.ClosedAt = null;
            }
        }

        // Six characters, with no 0/O or 1/l so a code is easy to read out loud.
        private async Task<string> GenerateCodeAsync()
        {
            const string letters = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var code = new string(Enumerable.Range(0, 6)
                    .Select(_ => letters[Random.Shared.Next(letters.Length)])
                    .ToArray());

                if (!await _repository.CodeExistsAsync(code))
                {
                    return code;
                }
            }

            throw new InvalidOperationException("Could not generate a free poll code.");
        }
    }
}
