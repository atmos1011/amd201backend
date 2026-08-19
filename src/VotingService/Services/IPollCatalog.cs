using PollBuilder.Contracts.Polls;

namespace PollBuilder.Voting.Services
{
    /// <summary>
    /// VotingService's view of PollService. Implemented over HTTP through the API gateway, following the
    /// same pattern as the microservices lab, and stubbed in unit tests so vote rules can be exercised
    /// without a second service running.
    /// </summary>
    public interface IPollCatalog
    {
        /// <summary>Fetches a poll, or null when PollService reports it does not exist.</summary>
        Task<PollDto?> GetPollAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tells PollService that this poll now has votes, so its creator can no longer edit the
        /// question or options. Fire-and-forget in effect: a failure here must not fail the vote.
        /// </summary>
        Task NotifyVotesRecordedAsync(string code, CancellationToken cancellationToken = default);
    }
}
