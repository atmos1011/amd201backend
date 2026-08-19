using PollBuilder.Contracts.Voting;

namespace PollBuilder.Voting.Services
{
    /// <summary>Vote and results business rules.</summary>
    public interface IVotingService
    {
        /// <summary>Records a vote and broadcasts new tallies. Issues a voter token if none was sent.</summary>
        Task<VoteResponse> VoteAsync(
            string code, int optionIndex, string? voterToken, CancellationToken cancellationToken = default);

        Task<PollResultsResponse> GetResultsAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>Whether the given respondent has already voted in this poll.</summary>
        Task<bool> HasVotedAsync(string code, string? voterToken, CancellationToken cancellationToken = default);
    }
}
