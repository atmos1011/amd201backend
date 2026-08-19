using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Polls;

namespace PollService.Tests.Integration
{
    /// <summary>
    /// End-to-end tests over the real HTTP surface: status codes, headers, validation responses and
    /// ProblemDetails bodies, which unit tests deliberately do not cover.
    /// </summary>
    public class PollsApiTests(PollApiFactory factory) : IClassFixture<PollApiFactory>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _client = factory.CreateClient();

        private static CreatePollRequest ValidRequest() => new()
        {
            Question = "Which framework?",
            Options = ["Vue", "React", "Svelte"]
        };

        private async Task<CreatedPollResponse> CreatePollAsync()
        {
            var response = await _client.PostAsJsonAsync("/api/polls", ValidRequest(), Json);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<CreatedPollResponse>(Json))!;
        }

        [Fact]
        public async Task Create_returns_201_with_a_location_header()
        {
            var response = await _client.PostAsJsonAsync("/api/polls", ValidRequest(), Json);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var created = await response.Content.ReadFromJsonAsync<CreatedPollResponse>(Json);
            Assert.NotNull(created);
            Assert.Equal("https://spa.test/poll/" + created.Code, created.ShareUrl);
        }

        [Theory]
        [InlineData("Hi", new[] { "A", "B" })]                       // question too short
        [InlineData("A real question?", new[] { "Only one" })]       // too few options
        [InlineData("A real question?", new[] { "A", "B", "C", "D", "E", "F", "G" })] // too many options
        [InlineData("A real question?", new[] { "Same", "same" })]   // duplicate options
        [InlineData("A real question?", new[] { "A", "  " })]        // blank option
        public async Task Create_rejects_invalid_input_with_400(string question, string[] options)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/polls", new CreatePollRequest { Question = question, Options = options }, Json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Get_returns_the_poll_and_404_for_an_unknown_code()
        {
            var created = await CreatePollAsync();

            var found = await _client.GetFromJsonAsync<PollDto>($"/api/polls/{created.Code}", Json);
            Assert.NotNull(found);
            Assert.Equal("Which framework?", found.Question);
            Assert.Equal(3, found.Options.Count);
            Assert.True(found.AcceptsVotes);

            var missing = await _client.GetAsync("/api/polls/zzzzzz");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        [Fact]
        public async Task Get_never_leaks_the_creator_token()
        {
            var created = await CreatePollAsync();

            var body = await _client.GetStringAsync($"/api/polls/{created.Code}");

            Assert.DoesNotContain(created.CreatorToken, body, StringComparison.Ordinal);
            Assert.DoesNotContain("creatorToken", body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Close_without_the_creator_token_is_403_and_leaves_the_poll_open()
        {
            var created = await CreatePollAsync();

            var response = await _client.PostAsync($"/api/polls/{created.Code}/close", content: null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            var poll = await _client.GetFromJsonAsync<PollDto>($"/api/polls/{created.Code}", Json);
            Assert.Equal(PollStatus.Open, poll!.Status);
        }

        [Fact]
        public async Task Close_with_the_creator_token_closes_the_poll()
        {
            var created = await CreatePollAsync();

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/polls/{created.Code}/close");
            request.Headers.Add(ServiceDefaults.CreatorTokenHeader, created.CreatorToken);
            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var poll = await response.Content.ReadFromJsonAsync<PollDto>(Json);
            Assert.Equal(PollStatus.Closed, poll!.Status);
            Assert.False(poll.AcceptsVotes);
        }

        [Fact]
        public async Task Patch_can_reopen_a_closed_poll()
        {
            var created = await CreatePollAsync();
            await SendWithCreatorTokenAsync(HttpMethod.Post, $"/api/polls/{created.Code}/close", created.CreatorToken);

            var response = await SendWithCreatorTokenAsync(
                HttpMethod.Patch,
                $"/api/polls/{created.Code}",
                created.CreatorToken,
                new PatchPollRequest { Status = PollStatus.Open });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var poll = await response.Content.ReadFromJsonAsync<PollDto>(Json);
            Assert.Equal(PollStatus.Open, poll!.Status);
        }

        [Fact]
        public async Task Put_replaces_the_question_and_options()
        {
            var created = await CreatePollAsync();

            var response = await SendWithCreatorTokenAsync(
                HttpMethod.Put,
                $"/api/polls/{created.Code}",
                created.CreatorToken,
                new UpdatePollRequest
                {
                    Question = "Which database?",
                    Options = ["PostgreSQL", "SQL Server"],
                    Status = PollStatus.Open
                });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var poll = await response.Content.ReadFromJsonAsync<PollDto>(Json);
            Assert.Equal("Which database?", poll!.Question);
            Assert.Equal(["PostgreSQL", "SQL Server"], poll.Options.Select(o => o.Text));
        }

        [Fact]
        public async Task Patch_with_an_empty_body_is_rejected()
        {
            var created = await CreatePollAsync();

            var response = await SendWithCreatorTokenAsync(
                HttpMethod.Patch, $"/api/polls/{created.Code}", created.CreatorToken, new PatchPollRequest());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Editing_is_refused_once_the_internal_callback_reports_votes()
        {
            var created = await CreatePollAsync();

            using var callback = new HttpRequestMessage(
                HttpMethod.Post, $"/internal/polls/{created.Code}/votes-recorded");
            callback.Headers.Add(ServiceDefaults.InternalApiKeyHeader, PollApiFactory.TestInternalApiKey);
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(callback)).StatusCode);

            var response = await SendWithCreatorTokenAsync(
                HttpMethod.Patch,
                $"/api/polls/{created.Code}",
                created.CreatorToken,
                new PatchPollRequest { Question = "A different question?" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Internal_endpoints_reject_callers_without_the_shared_secret()
        {
            var created = await CreatePollAsync();

            var response = await _client.PostAsync(
                $"/internal/polls/{created.Code}/votes-recorded", content: null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Errors_are_returned_as_problem_details_with_a_machine_readable_code()
        {
            var response = await _client.GetAsync("/api/polls/zzzzzz");

            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("poll_not_found", document.RootElement.GetProperty("errorCode").GetString());
        }

        [Fact]
        public async Task Qr_endpoint_returns_a_png()
        {
            var created = await CreatePollAsync();

            var response = await _client.GetAsync($"/api/polls/{created.Code}/qr");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal<byte[]>([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
        }

        private async Task<HttpResponseMessage> SendWithCreatorTokenAsync(
            HttpMethod method, string path, string creatorToken, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add(ServiceDefaults.CreatorTokenHeader, creatorToken);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);
            }

            return await _client.SendAsync(request);
        }
    }
}
