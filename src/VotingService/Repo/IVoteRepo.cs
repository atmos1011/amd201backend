using PollBuilder.Voting.Models;

namespace PollBuilder.Voting.Repo
{
    /// <summary>Vote count for one option index.</summary>
    public record OptionTally(int OptionIndex, int Count);

    /// <summary>
    /// Persistence boundary for votes. Keeping EF Core behind this interface is what lets the domain
    /// service be unit-tested against an in-memory fake with no database at all.
    /// </summary>
    public interface IVoteRepo
    {
        Task<bool> HasVotedAsync(string pollCode, string voterToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a vote. Returns false when the unique (PollCode, VoterToken) index rejects it, which
        /// is how a genuine double-submit race is caught rather than by a check-then-insert.
        /// </summary>
        Task<bool> TryAddVoteAsync(Vote vote, CancellationToken cancellationToken = default);

        /// <summary>Vote counts grouped by option index. Options with no votes are omitted.</summary>
        Task<IReadOnlyList<OptionTally>> GetTallyAsync(string pollCode, CancellationToken cancellationToken = default);

        Task<int> CountVotesAsync(string pollCode, CancellationToken cancellationToken = default);
    }
}
