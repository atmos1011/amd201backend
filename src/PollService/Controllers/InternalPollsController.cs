using Microsoft.AspNetCore.Mvc;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Polls.Repo;
using PollBuilder.Polls.Services;

namespace PollBuilder.Polls.Controllers
{
    /// <summary>
    /// Service-to-service endpoints. Not routed by the gateway and guarded by a shared secret, so they
    /// are not part of the public API surface the SPA sees.
    /// </summary>
    [ApiController]
    [Route("internal/polls")]
    [InternalOnly]
    public class InternalPollsController : ControllerBase
    {
        private readonly IPollService _pollService;

        public InternalPollsController(IPollService pollService)
        {
            _pollService = pollService;
        }

        /// <summary>
        /// Called by VotingService when a vote is recorded. PollService stores no votes, so this is how
        /// it learns that editing the question or options would now rewrite history for existing voters.
        /// </summary>
        [HttpPost("{code}/votes-recorded")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> VotesRecorded(string code, CancellationToken cancellationToken)
        {
            await _pollService.MarkHasVotesAsync(code, cancellationToken);
            return NoContent();
        }
    }
}
