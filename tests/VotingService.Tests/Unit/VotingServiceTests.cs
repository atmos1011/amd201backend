using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PollBuilder.Contracts.Errors;

namespace VotingService.Tests.Unit
{
    /// <summary>
    /// Business-rule tests for voting: one vote per respondent, valid options only, open polls only,
    /// and correct tallies. PollService is stubbed, so a failure here is a voting bug, not a wiring bug.
    /// </summary>
    public class VotingServiceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeVoteRepo _repository = new();
        private readonly StubPollCatalog _polls = new();
        private readonly SpyNotifier _notifier = new();
        private readonly FakeTimeProvider _time = new(Now);

        private PollBuilder.Voting.Services.VotingService CreateSut() =>
            new(_repository, _polls, _notifier, _time,
                NullLogger<PollBuilder.Voting.Services.VotingService>.Instance);

        [Fact]
        public async Task Voting_without_a_token_issues_one_and_returns_it()
        {
            _polls.Add(StubPollCatalog.OpenPoll());

            var result = await CreateSut().VoteAsync("abc123", optionIndex: 1, voterToken: null);

            Assert.False(string.IsNullOrWhiteSpace(result.VoterToken));
            Assert.Equal(1, result.Results.TotalVotes);
        }

        [Fact]
        public async Task A_respondent_can_only_vote_once()
        {
            _polls.Add(StubPollCatalog.OpenPoll());
            var sut = CreateSut();

            var first = await sut.VoteAsync("abc123", optionIndex: 0, voterToken: null);

            await Assert.ThrowsAsync<DuplicateVoteException>(
                () => sut.VoteAsync("abc123", optionIndex: 1, voterToken: first.VoterToken));
            Assert.Single(_repository.Votes);
        }

        [Fact]
        public async Task Different_respondents_vote_independently()
        {
            _polls.Add(StubPollCatalog.OpenPoll());
            var sut = CreateSut();

            await sut.VoteAsync("abc123", optionIndex: 0, voterToken: "browser-one");
            var second = await sut.VoteAsync("abc123", optionIndex: 0, voterToken: "browser-two");

            Assert.Equal(2, second.Results.TotalVotes);
            Assert.Equal(2, second.Results.Options[0].Votes);
        }

        [Fact]
        public async Task Voting_on_a_closed_poll_is_refused()
        {
            _polls.Add(StubPollCatalog.ClosedPoll());

            await Assert.ThrowsAsync<PollClosedException>(
                () => CreateSut().VoteAsync("abc123", optionIndex: 0, voterToken: null));
            Assert.Empty(_repository.Votes);
        }

        [Fact]
        public async Task Voting_on_an_unknown_poll_is_a_not_found()
        {
            await Assert.ThrowsAsync<PollNotFoundException>(
                () => CreateSut().VoteAsync("nope01", optionIndex: 0, voterToken: null));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(-1)]
        [InlineData(99)]
        public async Task Voting_for_an_option_that_does_not_exist_is_refused(int optionIndex)
        {
            _polls.Add(StubPollCatalog.OpenPoll(optionCount: 3));

            await Assert.ThrowsAsync<InvalidOptionException>(
                () => CreateSut().VoteAsync("abc123", optionIndex, voterToken: null));
        }

        [Fact]
        public async Task A_successful_vote_broadcasts_the_new_tallies()
        {
            _polls.Add(StubPollCatalog.OpenPoll());

            await CreateSut().VoteAsync("abc123", optionIndex: 2, voterToken: null);

            var broadcast = Assert.Single(_notifier.Broadcasts);
            Assert.Equal("abc123", broadcast.Code);
            Assert.Equal(1, broadcast.Options[2].Votes);
        }

        [Fact]
        public async Task A_rejected_vote_broadcasts_nothing()
        {
            _polls.Add(StubPollCatalog.ClosedPoll());

            await Assert.ThrowsAsync<PollClosedException>(
                () => CreateSut().VoteAsync("abc123", optionIndex: 0, voterToken: null));

            Assert.Empty(_notifier.Broadcasts);
        }

        [Fact]
        public async Task The_first_vote_tells_PollService_to_lock_the_poll_for_editing()
        {
            _polls.Add(StubPollCatalog.OpenPoll());

            await CreateSut().VoteAsync("abc123", optionIndex: 0, voterToken: "browser-one");

            Assert.Equal(1, _polls.VotesRecordedNotifications);
        }

        [Fact]
        public async Task Results_list_every_option_including_ones_with_no_votes()
        {
            _polls.Add(StubPollCatalog.OpenPoll(optionCount: 3));
            var sut = CreateSut();
            await sut.VoteAsync("abc123", optionIndex: 0, voterToken: "a");

            var results = await sut.GetResultsAsync("abc123");

            Assert.Equal(3, results.Options.Count);
            Assert.Equal([1, 0, 0], results.Options.Select(o => o.Votes));
        }

        [Fact]
        public async Task Percentages_are_calculated_against_the_total()
        {
            _polls.Add(StubPollCatalog.OpenPoll(optionCount: 2));
            var sut = CreateSut();
            await sut.VoteAsync("abc123", optionIndex: 0, voterToken: "a");
            await sut.VoteAsync("abc123", optionIndex: 0, voterToken: "b");
            await sut.VoteAsync("abc123", optionIndex: 1, voterToken: "c");

            var results = await sut.GetResultsAsync("abc123");

            Assert.Equal(3, results.TotalVotes);
            Assert.Equal(66.7, results.Options[0].Percentage);
            Assert.Equal(33.3, results.Options[1].Percentage);
        }

        [Fact]
        public async Task An_empty_poll_reports_zero_percent_rather_than_dividing_by_zero()
        {
            _polls.Add(StubPollCatalog.OpenPoll());

            var results = await CreateSut().GetResultsAsync("abc123");

            Assert.Equal(0, results.TotalVotes);
            Assert.All(results.Options, option => Assert.Equal(0, option.Percentage));
        }

        [Fact]
        public async Task HasVoted_is_false_for_a_respondent_with_no_token()
        {
            _polls.Add(StubPollCatalog.OpenPoll());

            Assert.False(await CreateSut().HasVotedAsync("abc123", voterToken: null));
        }

        [Fact]
        public async Task HasVoted_is_true_after_that_respondent_votes()
        {
            _polls.Add(StubPollCatalog.OpenPoll());
            var sut = CreateSut();
            await sut.VoteAsync("abc123", optionIndex: 0, voterToken: "browser-one");

            Assert.True(await sut.HasVotedAsync("abc123", "browser-one"));
            Assert.False(await sut.HasVotedAsync("abc123", "browser-two"));
        }

        [Fact]
        public async Task Votes_are_stamped_with_the_current_time()
        {
            _polls.Add(StubPollCatalog.OpenPoll());

            await CreateSut().VoteAsync("abc123", optionIndex: 0, voterToken: "a");

            Assert.Equal(Now, _repository.Votes[0].VotedAt);
        }
    }
}
