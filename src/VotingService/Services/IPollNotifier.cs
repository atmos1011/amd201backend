using PollBuilder.Contracts.Voting;

namespace PollBuilder.Voting.Services
{
    /// <summary>
    /// Realtime fan-out. VotingService does not host the SignalR hub itself; it hands the new tallies to
    /// RealtimeService, which pushes them to every browser watching that poll.
    /// </summary>
    public interface IPollNotifier
    {
        Task ResultsUpdatedAsync(PollResultsResponse results, CancellationToken cancellationToken = default);
    }
}
