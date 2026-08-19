using PollBuilder.Contracts.Polls;
using PollBuilder.Contracts.Voting;
using PollBuilder.Voting.Models;
using PollBuilder.Voting.Repo;
using PollBuilder.Voting.Services;

namespace VotingService.Tests.Unit
{
    /// <summary>
    /// In-memory vote store that reproduces the one thing the real database guarantees: the unique
    /// (PollCode, VoterToken) index. Without that, a duplicate-vote test would prove nothing.
    /// </summary>
    internal sealed class FakeVoteRepo : IVoteRepo
    {
        private readonly List<Vote> _votes = [];
        private int _nextId = 1;

        public IReadOnlyList<Vote> Votes => _votes;

        public Task<bool> HasVotedAsync(string pollCode, string voterToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(_votes.Exists(v => v.PollCode == pollCode && v.VoterToken == voterToken));

        public Task<bool> TryAddVoteAsync(Vote vote, CancellationToken cancellationToken = default)
        {
            if (_votes.Exists(v => v.PollCode == vote.PollCode && v.VoterToken == vote.VoterToken))
            {
                return Task.FromResult(false);
            }

            vote.Id = _nextId++;
            _votes.Add(vote);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<OptionTally>> GetTallyAsync(
            string pollCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OptionTally>>(
            [
                .. _votes.Where(v => v.PollCode == pollCode)
                         .GroupBy(v => v.OptionIndex)
                         .Select(g => new OptionTally(g.Key, g.Count()))
            ]);

        public Task<int> CountVotesAsync(string pollCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(_votes.Count(v => v.PollCode == pollCode));
    }

    /// <summary>Stands in for PollService so vote rules can be tested without a second service running.</summary>
    internal sealed class StubPollCatalog : IPollCatalog
    {
        private readonly Dictionary<string, PollDto> _polls = new(StringComparer.Ordinal);

        public int VotesRecordedNotifications { get; private set; }

        public StubPollCatalog Add(PollDto poll)
        {
            _polls[poll.Code] = poll;
            return this;
        }

        public static PollDto OpenPoll(string code = "abc123", int optionCount = 3) =>
            new(code,
                "Best language?",
                PollStatus.Open,
                DateTimeOffset.UnixEpoch,
                ExpiresAt: null,
                ClosedAt: null,
                AcceptsVotes: true,
                HasVotes: false,
                [.. Enumerable.Range(0, optionCount).Select(i => new PollOptionDto(i, $"Option {i}"))]);

        public static PollDto ClosedPoll(string code = "abc123") =>
            OpenPoll(code) with { Status = PollStatus.Closed, AcceptsVotes = false };

        public Task<PollDto?> GetPollAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(_polls.GetValueOrDefault(code));

        public Task NotifyVotesRecordedAsync(string code, CancellationToken cancellationToken = default)
        {
            VotesRecordedNotifications++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Records what would have been pushed over SignalR.</summary>
    internal sealed class SpyNotifier : IPollNotifier
    {
        public List<PollResultsResponse> Broadcasts { get; } = [];

        public Task ResultsUpdatedAsync(PollResultsResponse results, CancellationToken cancellationToken = default)
        {
            Broadcasts.Add(results);
            return Task.CompletedTask;
        }
    }
}
