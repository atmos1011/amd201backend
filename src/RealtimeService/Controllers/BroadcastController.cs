using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Realtime;
using PollBuilder.Realtime.Hubs;

namespace PollBuilder.Realtime.Controllers
{
    /// <summary>
    /// Internal entry point that turns an HTTP call from another service into a SignalR push. Keeping
    /// the hub in its own service means the browsers' long-lived WebSocket connections live in one
    /// process, and the other services stay plain request/response APIs.
    /// </summary>
    [ApiController]
    [Route("internal")]
    [InternalOnly]
    public class BroadcastController : ControllerBase
    {
        private readonly IHubContext<PollHub> _hubContext;
        private readonly ILogger<BroadcastController> _logger;

        public BroadcastController(IHubContext<PollHub> hubContext, ILogger<BroadcastController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>Called by VotingService after a vote: pushes new tallies to that poll's watchers.</summary>
        [HttpPost("broadcast")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Broadcast(
            [FromBody] BroadcastRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            await _hubContext.Clients
                .Group(PollHub.GroupFor(request.Results.Code))
                .SendAsync("ResultsUpdated", request.Results, cancellationToken);

            _logger.LogInformation("Broadcast ResultsUpdated for poll {Code}", request.Results.Code);
            return Accepted();
        }

        /// <summary>Called by PollService when a poll closes: tells watchers to stop accepting votes.</summary>
        [HttpPost("poll-closed")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> PollClosed(
            [FromBody] PollClosedNotification notification, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(notification);

            await _hubContext.Clients
                .Group(PollHub.GroupFor(notification.Code))
                .SendAsync("PollClosed", notification, cancellationToken);

            _logger.LogInformation("Broadcast PollClosed for poll {Code}", notification.Code);
            return Accepted();
        }
    }
}
