using Microsoft.EntityFrameworkCore;
using VoteManage.Data;
using VoteManage.Models;
using VoteManage.Repo;

namespace PollBuilder.Tests
{
    // Tests for the voting side: one vote per browser, and the counting that feeds the chart.
    public class VoteRepoTests
    {
        private static myContext NewContext()
        {
            var options = new DbContextOptionsBuilder<myContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new myContext(options);
        }

        private static Vote NewVote(string pollCode, int optionIndex, string voterToken)
        {
            return new Vote
            {
                PollCode = pollCode,
                OptionIndex = optionIndex,
                VoterToken = voterToken,
                VotedAt = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task AddAsync_saves_a_vote()
        {
            var repo = new VoteRepo(NewContext());

            var saved = await repo.AddAsync(NewVote("abc123", 0, "browser-a"));

            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
        }

        [Fact]
        public async Task HasVotedAsync_is_false_before_voting_and_true_afterwards()
        {
            var repo = new VoteRepo(NewContext());

            Assert.False(await repo.HasVotedAsync("abc123", "browser-a"));

            await repo.AddAsync(NewVote("abc123", 0, "browser-a"));

            Assert.True(await repo.HasVotedAsync("abc123", "browser-a"));
        }

        [Fact]
        public async Task One_browser_voting_does_not_block_another_browser()
        {
            var repo = new VoteRepo(NewContext());
            await repo.AddAsync(NewVote("abc123", 0, "browser-a"));

            Assert.False(await repo.HasVotedAsync("abc123", "browser-b"));
        }

        [Fact]
        public async Task Voting_in_one_poll_does_not_block_the_same_browser_in_another_poll()
        {
            var repo = new VoteRepo(NewContext());
            await repo.AddAsync(NewVote("poll01", 0, "browser-a"));

            Assert.False(await repo.HasVotedAsync("poll02", "browser-a"));
        }

        [Fact]
        public async Task GetByPollCodeAsync_returns_only_the_votes_for_that_poll()
        {
            var repo = new VoteRepo(NewContext());
            await repo.AddAsync(NewVote("poll01", 0, "browser-a"));
            await repo.AddAsync(NewVote("poll01", 1, "browser-b"));
            await repo.AddAsync(NewVote("poll02", 0, "browser-c"));

            var votes = await repo.GetByPollCodeAsync("poll01");

            Assert.Equal(2, votes.Count());
        }

        [Fact]
        public async Task Counting_votes_per_option_gives_the_numbers_the_chart_shows()
        {
            var repo = new VoteRepo(NewContext());
            await repo.AddAsync(NewVote("poll01", 0, "browser-a"));
            await repo.AddAsync(NewVote("poll01", 0, "browser-b"));
            await repo.AddAsync(NewVote("poll01", 1, "browser-c"));

            var votes = (await repo.GetByPollCodeAsync("poll01")).ToList();

            Assert.Equal(3, votes.Count);
            Assert.Equal(2, votes.Count(v => v.OptionIndex == 0));
            Assert.Equal(1, votes.Count(v => v.OptionIndex == 1));
            Assert.Equal(0, votes.Count(v => v.OptionIndex == 2));
        }
    }
}
