using Microsoft.EntityFrameworkCore;
using PollBuilder.Voting.Data;
using PollBuilder.Voting.Models;
using PollBuilder.Voting.Repo;

namespace PollBuilder.Voting.Repo
{
    /// <summary>EF Core implementation of <see cref="IVoteRepo"/>.</summary>
    public class VoteRepo : IVoteRepo
    {
        private readonly VotingDbContext _context;

        public VoteRepo(VotingDbContext context)
        {
            _context = context;
        }

        public Task<bool> HasVotedAsync(string pollCode, string voterToken, CancellationToken cancellationToken = default) =>
            _context.Votes.AnyAsync(v => v.PollCode == pollCode && v.VoterToken == voterToken, cancellationToken);

        public async Task<bool> TryAddVoteAsync(Vote vote, CancellationToken cancellationToken = default)
        {
            await _context.Votes.AddAsync(vote, cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException)
            {
                // The unique (PollCode, VoterToken) index rejected the insert: this browser already
                // voted. Detach so the failed entity does not poison later saves on this scope.
                _context.Entry(vote).State = EntityState.Detached;
                return false;
            }
        }

        public async Task<IReadOnlyList<OptionTally>> GetTallyAsync(
            string pollCode, CancellationToken cancellationToken = default)
        {
            var tallies = await _context.Votes
                .Where(v => v.PollCode == pollCode)
                .GroupBy(v => v.OptionIndex)
                .Select(g => new OptionTally(g.Key, g.Count()))
                .ToListAsync(cancellationToken)
                ;

            return tallies;
        }

        public Task<int> CountVotesAsync(string pollCode, CancellationToken cancellationToken = default) =>
            _context.Votes.CountAsync(v => v.PollCode == pollCode, cancellationToken);
    }
}
