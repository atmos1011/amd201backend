using PollBuilder.Contracts.Polls;
using PollBuilder.Contracts.Voting;

namespace PollBuilder.Contracts.Realtime
{
    /// <summary>
    /// Internal service-to-service payload: VotingService posts this to RealtimeService, which fans it
    /// out over SignalR as <c>ResultsUpdated</c> to everyone watching the poll. Not public API.
    /// </summary>
    public record BroadcastRequest(PollResultsResponse Results);

    /// <summary>
    /// Internal payload sent by PollService when a poll closes, fanned out as <c>PollClosed</c>.
    /// </summary>
    /// <remarks>
    /// It carries no tallies, because PollService does not own votes. The SPA reacts by disabling the
    /// vote form and re-fetching results, which keeps the service boundary intact rather than making
    /// PollService call VotingService just to fill in numbers it has no business knowing.
    /// </remarks>
    public record PollClosedNotification(string Code, PollStatus Status, DateTimeOffset? ClosedAt);
}
