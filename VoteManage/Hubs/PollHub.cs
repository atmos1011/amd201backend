using Microsoft.AspNetCore.SignalR;

namespace VoteManage.Hubs
{
    // The results page opens a WebSocket to this hub. Everyone watching the same poll joins
    // the same group, so a new vote is only sent to the people looking at that poll.
    public class PollHub : Hub
    {
        public static string GroupFor(string code) => "poll-" + code;

        public async Task JoinPoll(string code)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(code));
        }

        public async Task LeavePoll(string code)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(code));
        }
    }
}
