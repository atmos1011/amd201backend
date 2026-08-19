using Microsoft.EntityFrameworkCore;
using PollBuilder.Polls.Data;
using PollBuilder.Polls.Models;
using PollBuilder.Polls.Repo;

namespace PollBuilder.Polls.Repo
{
    /// <summary>EF Core implementation of <see cref="IPollRepo"/>.</summary>
    public class PollRepo : IPollRepo
    {
        private readonly PollDbContext _context;

        public PollRepo(PollDbContext context)
        {
            _context = context;
        }

        public Task<Poll?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            _context.Polls
                .Include(p => p.Options.OrderBy(o => o.OptionIndex))
                .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
            _context.Polls.AnyAsync(p => p.Code == code, cancellationToken);

        public async Task AddPollAsync(Poll poll, CancellationToken cancellationToken = default) =>
            await _context.Polls.AddAsync(poll, cancellationToken);

        public async Task ReplaceOptionsAsync(
            Poll poll, IReadOnlyList<string> options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(poll);
            ArgumentNullException.ThrowIfNull(options);

            var existing = await _context.PollOptions
                .Where(o => o.PollId == poll.Id)
                .ToListAsync(cancellationToken)
                ;

            _context.PollOptions.RemoveRange(existing);

            // Flush the deletes before inserting: the new rows reuse the same (PollId, OptionIndex)
            // values, which would otherwise trip the unique index inside a single batch.
            await _context.SaveChangesAsync(cancellationToken);

            poll.Options = [.. options.Select((text, index) => new PollOption
            {
                PollId = poll.Id,
                OptionIndex = index,
                Text = text.Trim()
            })];

            await _context.PollOptions.AddRangeAsync(poll.Options, cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
