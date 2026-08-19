using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Polls;
using PollBuilder.Contracts.Voting;

namespace VotingService.Tests.Integration
{
    /// <summary>
    /// End-to-end tests over the real HTTP surface, including the full journey the demo follows:
    /// vote, vote again, read results, close, vote again.
    /// </summary>
    public class VotesApiTests : IClassFixture<VotingApiFactory>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly VotingApiFactory _factory;
        private readonly HttpClient _client;

        public VotesApiTests(VotingApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        /// <summary>Each test gets its own poll code so tests never see each other's votes.</summary>
        private string GivenAnOpenPoll(int optionCount = 3)
        {
            var code = "p" + Guid.NewGuid().ToString("N")[..5];
            _factory.Polls.Set(new PollDto(
                code,
                "Best language?",
                PollStatus.Open,
                DateTimeOffset.UnixEpoch,
                ExpiresAt: null,
                ClosedAt: null,
                AcceptsVotes: true,
                HasVotes: false,
                [.. Enumerable.Range(0, optionCount).Select(i => new PollOptionDto(i, $"Option {i}"))]));
            return code;
        }

        private async Task<HttpResponseMessage> VoteAsync(string code, int optionIndex, string? voterToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/votes/{code}")
            {
                Content = JsonContent.Create(new VoteRequest { OptionIndex = optionIndex }, options: Json)
            };

            if (voterToken is not null)
            {
                request.Headers.Add(ServiceDefaults.VoterTokenHeader, voterToken);
            }

            return await _client.SendAsync(request);
        }

        [Fact]
        public async Task A_first_vote_succeeds_and_the_token_comes_back_in_a_header()
        {
            var code = GivenAnOpenPoll();

            var response = await VoteAsync(code, optionIndex: 1, voterToken: null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains(ServiceDefaults.VoterTokenHeader));

            var body = await response.Content.ReadFromJsonAsync<VoteResponse>(Json);
            Assert.Equal(1, body!.Results.TotalVotes);
            Assert.Equal(1, body.Results.Options[1].Votes);
        }

        [Fact]
        public async Task Voting_twice_from_the_same_browser_returns_409()
        {
            var code = GivenAnOpenPoll();
            var first = await VoteAsync(code, optionIndex: 0, voterToken: null);
            var token = (await first.Content.ReadFromJsonAsync<VoteResponse>(Json))!.VoterToken;

            var second = await VoteAsync(code, optionIndex: 2, voterToken: token);

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

            using var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            Assert.Equal("already_voted", problem.RootElement.GetProperty("errorCode").GetString());
        }

        [Fact]
        public async Task Voting_for_an_option_outside_the_poll_returns_400()
        {
            var code = GivenAnOpenPoll(optionCount: 3);

            var response = await VoteAsync(code, optionIndex: 5, voterToken: "browser-x");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Voting_on_an_unknown_poll_returns_404()
        {
            var response = await VoteAsync("zzzzzz", optionIndex: 0, voterToken: "browser-x");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Results_reflect_votes_from_several_browsers()
        {
            var code = GivenAnOpenPoll(optionCount: 2);
            await VoteAsync(code, 0, "browser-a");
            await VoteAsync(code, 0, "browser-b");
            await VoteAsync(code, 1, "browser-c");

            var results = await _client.GetFromJsonAsync<PollResultsResponse>($"/api/votes/{code}/results", Json);

            Assert.Equal(3, results!.TotalVotes);
            Assert.Equal(2, results.Options[0].Votes);
            Assert.Equal(1, results.Options[1].Votes);
        }

        [Fact]
        public async Task The_me_endpoint_reports_whether_this_browser_has_voted()
        {
            var code = GivenAnOpenPoll();

            using var before = new HttpRequestMessage(HttpMethod.Get, $"/api/votes/{code}/me");
            before.Headers.Add(ServiceDefaults.VoterTokenHeader, "browser-z");
            using var beforeDocument = JsonDocument.Parse(
                await (await _client.SendAsync(before)).Content.ReadAsStringAsync());
            Assert.False(beforeDocument.RootElement.GetProperty("hasVoted").GetBoolean());

            await VoteAsync(code, 0, "browser-z");

            using var after = new HttpRequestMessage(HttpMethod.Get, $"/api/votes/{code}/me");
            after.Headers.Add(ServiceDefaults.VoterTokenHeader, "browser-z");
            using var afterDocument = JsonDocument.Parse(
                await (await _client.SendAsync(after)).Content.ReadAsStringAsync());
            Assert.True(afterDocument.RootElement.GetProperty("hasVoted").GetBoolean());
        }

        [Fact]
        public async Task Closing_the_poll_stops_further_votes()
        {
            var code = GivenAnOpenPoll();
            Assert.Equal(HttpStatusCode.OK, (await VoteAsync(code, 0, "browser-early")).StatusCode);

            _factory.Polls.Close(code);

            var response = await VoteAsync(code, 0, "browser-late");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("poll_closed", problem.RootElement.GetProperty("errorCode").GetString());
        }

        [Fact]
        public async Task Results_are_still_readable_after_the_poll_closes()
        {
            var code = GivenAnOpenPoll();
            await VoteAsync(code, 0, "browser-a");
            _factory.Polls.Close(code);

            var results = await _client.GetFromJsonAsync<PollResultsResponse>($"/api/votes/{code}/results", Json);

            Assert.Equal(PollStatus.Closed, results!.Status);
            Assert.False(results.AcceptsVotes);
            Assert.Equal(1, results.TotalVotes);
        }

        [Fact]
        public async Task Results_can_be_downloaded_as_csv()
        {
            var code = GivenAnOpenPoll(optionCount: 2);
            await VoteAsync(code, 0, "browser-a");

            var response = await _client.GetAsync($"/api/votes/{code}/results.csv");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

            var lines = (await response.Content.ReadAsStringAsync()).Trim().Split('\n');
            Assert.Equal("option_index,option_text,votes,percentage", lines[0].Trim());
            Assert.Equal("0,Option 0,1,100.0", lines[1].Trim());
        }
    }
}
