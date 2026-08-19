using PollBuilder.Polls.Models;
using PollBuilder.Polls.Repo;
using PollBuilder.Polls.Services;

namespace PollService.Tests.Unit
{
    /// <summary>
    /// In-memory stand-in for the EF repository. Unit tests exercise the business rules through this,
    /// so they run in milliseconds and fail for domain reasons rather than database reasons.
    /// </summary>
    internal sealed class FakePollRepo : IPollRepo
    {
        private readonly List<Poll> _polls = [];
        private int _nextId = 1;

        public int SaveCount { get; private set; }

        public Task<Poll?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(_polls.Find(p => p.Code == code));

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(_polls.Exists(p => p.Code == code));

        public Task AddPollAsync(Poll poll, CancellationToken cancellationToken = default)
        {
            poll.Id = _nextId++;
            _polls.Add(poll);
            return Task.CompletedTask;
        }

        public Task ReplaceOptionsAsync(
            Poll poll, IReadOnlyList<string> options, CancellationToken cancellationToken = default)
        {
            poll.Options = [.. options.Select((text, index) => new PollOption
            {
                PollId = poll.Id,
                OptionIndex = index,
                Text = text.Trim()
            })];
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Returns a scripted sequence of codes so collision handling can be tested deterministically.</summary>
    internal sealed class ScriptedCodeGenerator(params string[] codes) : IPollCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public int CallCount { get; private set; }

        public string Next()
        {
            CallCount++;
            return _codes.Count > 0 ? _codes.Dequeue() : $"code{CallCount}";
        }
    }

    /// <summary>Records the close notifications that would have gone to RealtimeService.</summary>
    internal sealed class SpyRealtimeNotifier : IRealtimeNotifier
    {
        public List<string> ClosedPolls { get; } = [];

        public Task PollClosedAsync(string code, DateTimeOffset? closedAt, CancellationToken cancellationToken = default)
        {
            ClosedPolls.Add(code);
            return Task.CompletedTask;
        }
    }
}
