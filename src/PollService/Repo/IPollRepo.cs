using PollBuilder.Polls.Models;

namespace PollBuilder.Polls.Repo
{
    /// <summary>
    /// Persistence boundary for polls. Keeping EF Core behind this interface is what lets the domain
    /// service be unit-tested against an in-memory fake with no database at all.
    /// </summary>
    public interface IPollRepo
    {
        /// <summary>Loads a poll and its options by public code, or null if it does not exist.</summary>
        Task<Poll?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

        Task AddPollAsync(Poll poll, CancellationToken cancellationToken = default);

        /// <summary>Replaces a poll's option rows wholesale. Only legal while the poll has no votes.</summary>
        Task ReplaceOptionsAsync(Poll poll, IReadOnlyList<string> options, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
