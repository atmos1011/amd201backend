using Microsoft.EntityFrameworkCore;
using VoteManage.Data;
using VoteManage.Models;

namespace VoteManage.Repo
{
    public class VoteRepo : IVoteRepo
    {
        private readonly myContext _context;

        public VoteRepo(myContext context)
        {
            _context = context;
        }

        public async Task<bool> HasVotedAsync(string pollCode, string voterToken) =>
            await _context.Votes.AnyAsync(v => v.PollCode == pollCode && v.VoterToken == voterToken);

        // Returns null when the unique index rejects the vote, which means this browser
        // already voted in this poll.
        public async Task<Vote?> AddAsync(Vote vote)
        {
            _context.Votes.Add(vote);
            try
            {
                await _context.SaveChangesAsync();
                return vote;
            }
            catch (DbUpdateException)
            {
                _context.Entry(vote).State = EntityState.Detached;
                return null;
            }
        }

        public async Task<IEnumerable<Vote>> GetByPollCodeAsync(string pollCode) =>
            await _context.Votes.Where(v => v.PollCode == pollCode).ToListAsync();
    }
}
