using Microsoft.EntityFrameworkCore;
using PollManage.Data;
using PollManage.Models;

namespace PollManage.Repo
{
    public class PollRepo : IPollRepo
    {
        private readonly myContext _context;

        public PollRepo(myContext context)
        {
            _context = context;
        }

        public async Task<Poll?> GetByCodeAsync(string code) =>
            await _context.Polls
                .Include(p => p.Options)
                .FirstOrDefaultAsync(p => p.Code == code);

        public async Task<bool> CodeExistsAsync(string code) =>
            await _context.Polls.AnyAsync(p => p.Code == code);

        public async Task<Poll> AddAsync(Poll poll)
        {
            _context.Polls.Add(poll);
            await _context.SaveChangesAsync();
            return poll;
        }

        public async Task<Poll?> UpdateAsync(Poll poll)
        {
            _context.Polls.Update(poll);
            await _context.SaveChangesAsync();
            return poll;
        }

        public async Task<Poll?> MarkHasVotesAsync(string code)
        {
            var poll = await GetByCodeAsync(code);
            if (poll == null)
            {
                return null;
            }

            poll.HasVotes = true;
            await _context.SaveChangesAsync();
            return poll;
        }
    }
}
