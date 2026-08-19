using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PollBuilder.Contracts.Errors;
using PollBuilder.Contracts.Polls;
using PollBuilder.Polls.Services;

namespace PollService.Tests.Unit
{
    /// <summary>
    /// Business-rule tests for poll creation, editing and the open/closed lifecycle. No database, no
    /// HTTP: the rules are exercised directly so a failure points at the rule, not the plumbing.
    /// </summary>
    public class PollServiceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePollRepo _repository = new();
        private readonly SpyRealtimeNotifier _notifier = new();
        private readonly FakeTimeProvider _time = new(Now);

        private PollBuilder.Polls.Services.PollService CreateSut(params string[] codes) =>
            new(_repository,
                new ScriptedCodeGenerator(codes.Length > 0 ? codes : ["abc123"]),
                _notifier,
                _time,
                NullLogger<PollBuilder.Polls.Services.PollService>.Instance);

        private static CreatePollRequest ValidRequest() => new()
        {
            Question = "Best language?",
            Options = ["C#", "TypeScript", "Go"]
        };

        [Fact]
        public async Task CreateAsync_returns_a_share_url_and_a_one_time_creator_token()
        {
            var sut = CreateSut("abc123");

            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");

            Assert.Equal("abc123", created.Code);
            Assert.Equal("https://spa.example/poll/abc123", created.ShareUrl);
            Assert.False(string.IsNullOrWhiteSpace(created.CreatorToken));
            Assert.Equal(3, created.Options.Count);
            Assert.Equal(PollStatus.Open, created.Status);
        }

        [Fact]
        public async Task CreateAsync_numbers_options_from_zero_in_submitted_order()
        {
            var created = await CreateSut().CreateAsync(ValidRequest(), "https://spa.example");

            Assert.Equal([0, 1, 2], created.Options.Select(o => o.Index));
            Assert.Equal(["C#", "TypeScript", "Go"], created.Options.Select(o => o.Text));
        }

        [Fact]
        public async Task CreateAsync_retries_when_a_generated_code_is_already_taken()
        {
            var sut = CreateSut("taken1", "free01");
            await CreateSut("taken1").CreateAsync(ValidRequest(), "https://spa.example");

            // Same repository, so "taken1" now exists and the generator must be asked again.
            var second = await sut.CreateAsync(ValidRequest(), "https://spa.example");

            Assert.Equal("free01", second.Code);
        }

        [Fact]
        public async Task CreateAsync_gives_up_after_repeated_collisions()
        {
            await CreateSut("taken1").CreateAsync(ValidRequest(), "https://spa.example");
            var sut = CreateSut("taken1", "taken1", "taken1", "taken1", "taken1");

            await Assert.ThrowsAsync<PollCodeGenerationException>(
                () => sut.CreateAsync(ValidRequest(), "https://spa.example"));
        }

        [Fact]
        public async Task GetAsync_throws_for_an_unknown_code()
        {
            await Assert.ThrowsAsync<PollNotFoundException>(() => CreateSut().GetAsync("nope01"));
        }

        [Fact]
        public async Task CloseAsync_requires_the_creator_token()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");

            await Assert.ThrowsAsync<NotPollCreatorException>(() => sut.CloseAsync(created.Code, "not-the-token"));
            await Assert.ThrowsAsync<NotPollCreatorException>(() => sut.CloseAsync(created.Code, creatorToken: null));
        }

        [Fact]
        public async Task CloseAsync_stops_the_poll_accepting_votes()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");

            var closed = await sut.CloseAsync(created.Code, created.CreatorToken);

            Assert.Equal(PollStatus.Closed, closed.Status);
            Assert.False(closed.AcceptsVotes);
            Assert.Equal(Now, closed.ClosedAt);
        }

        [Fact]
        public async Task A_poll_past_its_expiry_closes_itself_on_the_next_read()
        {
            var sut = CreateSut();
            var request = ValidRequest();
            request.ExpiresAt = Now.AddMinutes(10);
            var created = await sut.CreateAsync(request, "https://spa.example");

            _time.SetUtcNow(Now.AddMinutes(11));
            var poll = await sut.GetAsync(created.Code);

            Assert.Equal(PollStatus.Closed, poll.Status);
            Assert.False(poll.AcceptsVotes);
        }

        [Fact]
        public async Task Reopening_an_expired_poll_clears_the_expiry_so_it_does_not_close_again()
        {
            var sut = CreateSut();
            var request = ValidRequest();
            request.ExpiresAt = Now.AddMinutes(10);
            var created = await sut.CreateAsync(request, "https://spa.example");

            _time.SetUtcNow(Now.AddMinutes(11));
            var reopened = await sut.PatchAsync(
                created.Code, new PatchPollRequest { Status = PollStatus.Open }, created.CreatorToken);

            Assert.Equal(PollStatus.Open, reopened.Status);
            Assert.Null(reopened.ExpiresAt);
            Assert.True(reopened.AcceptsVotes);
        }

        [Fact]
        public async Task PatchAsync_changes_only_the_fields_it_is_given()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");

            var patched = await sut.PatchAsync(
                created.Code, new PatchPollRequest { Question = "Best runtime?" }, created.CreatorToken);

            Assert.Equal("Best runtime?", patched.Question);
            Assert.Equal(3, patched.Options.Count);
            Assert.Equal(PollStatus.Open, patched.Status);
        }

        [Fact]
        public async Task ReplaceAsync_overwrites_the_options()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");

            var replaced = await sut.ReplaceAsync(
                created.Code,
                new UpdatePollRequest
                {
                    Question = "Tabs or spaces?",
                    Options = ["Tabs", "Spaces"],
                    Status = PollStatus.Open
                },
                created.CreatorToken);

            Assert.Equal("Tabs or spaces?", replaced.Question);
            Assert.Equal(["Tabs", "Spaces"], replaced.Options.Select(o => o.Text));
        }

        [Fact]
        public async Task Editing_the_question_or_options_is_refused_once_a_vote_exists()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");
            await sut.MarkHasVotesAsync(created.Code);

            await Assert.ThrowsAsync<PollHasVotesException>(() => sut.PatchAsync(
                created.Code, new PatchPollRequest { Question = "Something else?" }, created.CreatorToken));

            await Assert.ThrowsAsync<PollHasVotesException>(() => sut.PatchAsync(
                created.Code, new PatchPollRequest { Options = ["A", "B"] }, created.CreatorToken));
        }

        [Fact]
        public async Task Closing_a_poll_that_has_votes_is_still_allowed()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");
            await sut.MarkHasVotesAsync(created.Code);

            var closed = await sut.CloseAsync(created.Code, created.CreatorToken);

            Assert.Equal(PollStatus.Closed, closed.Status);
        }

        [Fact]
        public async Task Submitting_the_unchanged_question_is_not_treated_as_an_edit()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");
            await sut.MarkHasVotesAsync(created.Code);

            // A SPA that PUTs the whole form back must still be able to close the poll.
            var result = await sut.ReplaceAsync(
                created.Code,
                new UpdatePollRequest
                {
                    Question = "Best language?",
                    Options = ["C#", "TypeScript", "Go"],
                    Status = PollStatus.Closed
                },
                created.CreatorToken);

            Assert.Equal(PollStatus.Closed, result.Status);
        }

        [Fact]
        public async Task Closing_a_poll_pushes_the_news_to_watchers()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");

            await sut.CloseAsync(created.Code, created.CreatorToken);

            Assert.Equal([created.Code], _notifier.ClosedPolls);
        }

        [Fact]
        public async Task Closing_an_already_closed_poll_does_not_push_again()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");
            await sut.CloseAsync(created.Code, created.CreatorToken);

            await sut.CloseAsync(created.Code, created.CreatorToken);

            Assert.Single(_notifier.ClosedPolls);
        }

        [Fact]
        public async Task Auto_closing_on_expiry_does_not_push()
        {
            var sut = CreateSut();
            var request = ValidRequest();
            request.ExpiresAt = Now.AddMinutes(10);
            var created = await sut.CreateAsync(request, "https://spa.example");

            _time.SetUtcNow(Now.AddMinutes(11));
            await sut.GetAsync(created.Code);

            // Nobody triggered it, and every client learns about it on its next request.
            Assert.Empty(_notifier.ClosedPolls);
        }

        [Fact]
        public async Task MarkHasVotesAsync_is_idempotent()
        {
            var sut = CreateSut();
            var created = await sut.CreateAsync(ValidRequest(), "https://spa.example");

            await sut.MarkHasVotesAsync(created.Code);
            var savesAfterFirst = _repository.SaveCount;
            await sut.MarkHasVotesAsync(created.Code);

            Assert.Equal(savesAfterFirst, _repository.SaveCount);
        }
    }
}
