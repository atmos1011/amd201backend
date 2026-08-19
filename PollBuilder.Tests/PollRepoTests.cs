using Microsoft.EntityFrameworkCore;
using PollManage.Data;
using PollManage.Models;
using PollManage.Repo;

namespace PollBuilder.Tests
{
    // Tests for the poll side. Each test gets its own in-memory database so the tests
    // do not see each other's data.
    public class PollRepoTests
    {
        private static myContext NewContext()
        {
            var options = new DbContextOptionsBuilder<myContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new myContext(options);
        }

        private static Poll NewPoll(string code = "abc123")
        {
            return new Poll
            {
                Code = code,
                Question = "Best language?",
                Status = "Open",
                CreatedAt = DateTime.UtcNow,
                CreatorToken = "creator-token",
                Options =
                {
                    new PollOption { OptionIndex = 0, Text = "C#" },
                    new PollOption { OptionIndex = 1, Text = "JavaScript" }
                }
            };
        }

        [Fact]
        public async Task AddAsync_saves_the_poll_with_its_options()
        {
            var repo = new PollRepo(NewContext());

            var saved = await repo.AddAsync(NewPoll());

            Assert.True(saved.Id > 0);
            Assert.Equal(2, saved.Options.Count);
        }

        [Fact]
        public async Task GetByCodeAsync_finds_the_poll_and_loads_its_options()
        {
            var context = NewContext();
            var repo = new PollRepo(context);
            await repo.AddAsync(NewPoll("xyz789"));

            var found = await repo.GetByCodeAsync("xyz789");

            Assert.NotNull(found);
            Assert.Equal("Best language?", found.Question);
            Assert.Equal(2, found.Options.Count);
        }

        [Fact]
        public async Task GetByCodeAsync_returns_null_when_the_code_does_not_exist()
        {
            var repo = new PollRepo(NewContext());

            Assert.Null(await repo.GetByCodeAsync("nope00"));
        }

        [Fact]
        public async Task CodeExistsAsync_is_true_only_for_a_code_that_was_saved()
        {
            var repo = new PollRepo(NewContext());
            await repo.AddAsync(NewPoll("taken1"));

            Assert.True(await repo.CodeExistsAsync("taken1"));
            Assert.False(await repo.CodeExistsAsync("free01"));
        }

        [Fact]
        public async Task MarkHasVotesAsync_locks_the_poll_so_it_cannot_be_edited_later()
        {
            var context = NewContext();
            var repo = new PollRepo(context);
            await repo.AddAsync(NewPoll("lock01"));

            var poll = await repo.MarkHasVotesAsync("lock01");

            Assert.NotNull(poll);
            Assert.True(poll.HasVotes);
            Assert.True((await repo.GetByCodeAsync("lock01"))!.HasVotes);
        }

        [Fact]
        public async Task MarkHasVotesAsync_returns_null_for_a_poll_that_does_not_exist()
        {
            var repo = new PollRepo(NewContext());

            Assert.Null(await repo.MarkHasVotesAsync("nope00"));
        }

        [Fact]
        public async Task A_new_poll_starts_open_with_no_votes()
        {
            var repo = new PollRepo(NewContext());

            var saved = await repo.AddAsync(NewPoll());

            Assert.Equal("Open", saved.Status);
            Assert.False(saved.HasVotes);
            Assert.Null(saved.ClosedAt);
        }
    }
}
