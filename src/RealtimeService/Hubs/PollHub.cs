using Microsoft.AspNetCore.SignalR;

namespace PollBuilder.Realtime.Hubs
{
    /// <summary>
    /// WebSocket hub behind <c>/hubs/poll</c>. Browsers watching a results page join a group named after
    /// the poll code, so a vote fans out only to the people looking at that poll rather than to every
    /// connected client.
    /// </summary>
    public class PollHub : Hub
    {
        private readonly ILogger<PollHub> _logger;

        public PollHub(ILogger<PollHub> logger)
        {
            _logger = logger;
        }

        /// <summary>Group name for a poll. Also used by the broadcast endpoint.</summary>
        public static string GroupFor(string code) => $"poll:{code}";

        /// <summary>Called by the SPA when a results page opens.</summary>
        public async Task JoinPoll(string code)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(code));
            _logger.LogInformation("Connection {ConnectionId} joined poll {Code}", Context.ConnectionId, code);
        }

        /// <summary>Called by the SPA when the results page closes or navigates away.</summary>
        public Task LeavePoll(string code) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(code));
    }
}
