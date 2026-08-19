using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Polls;
using PollBuilder.Contracts.Realtime;
using PollBuilder.Contracts.Voting;

namespace RealtimeService.Tests
{
    /// <summary>
    /// Boots RealtimeService in-memory. It owns no database, so nothing needs substituting.
    /// </summary>
    public class RealtimeFactory : WebApplicationFactory<Program>
    {
        public const string TestInternalApiKey = "test-internal-key";

        public RealtimeFactory()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("ServiceEndpoints__InternalApiKey", TestInternalApiKey);
            Environment.SetEnvironmentVariable("Service__AllowedOrigins__0", "https://spa.test");
        }
    }

    /// <summary>
    /// Proves the realtime path end to end: a browser subscribes over SignalR, VotingService posts new
    /// tallies, and only the subscribers of that poll receive them. This is the piece the demo depends
    /// on, so it is worth testing rather than checking by eye.
    /// </summary>
    public class PollHubTests(RealtimeFactory factory) : IClassFixture<RealtimeFactory>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly RealtimeFactory _factory = factory;

        private static PollResultsResponse ResultsFor(string code, int votes) =>
            new(code,
                "Best language?",
                PollStatus.Open,
                AcceptsVotes: true,
                TotalVotes: votes,
                UpdatedAt: DateTimeOffset.UnixEpoch,
                [new OptionResultResponse(0, "C#", votes, 100)]);

        /// <summary>Connects a SignalR client through the in-memory test server rather than a real socket.</summary>
        private async Task<HubConnection> ConnectAsync()
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/poll"), options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
                .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
                .Build();

            await connection.StartAsync();
            return connection;
        }

        private Task<HttpResponseMessage> BroadcastAsync(BroadcastRequest request, string? apiKey) =>
            PostInternalAsync("/internal/broadcast", request, apiKey);

        private Task<HttpResponseMessage> PollClosedAsync(PollClosedNotification notification, string? apiKey) =>
            PostInternalAsync("/internal/poll-closed", notification, apiKey);

        private async Task<HttpResponseMessage> PostInternalAsync(string path, object body, string? apiKey)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body, body.GetType(), options: Json)
            };

            if (apiKey is not null)
            {
                message.Headers.Add(ServiceDefaults.InternalApiKeyHeader, apiKey);
            }

            return await _factory.CreateClient().SendAsync(message);
        }

        [Fact]
        public async Task A_client_watching_a_poll_receives_updated_results()
        {
            await using var connection = await ConnectAsync();
            var received = new TaskCompletionSource<PollResultsResponse>();
            connection.On<PollResultsResponse>("ResultsUpdated", results => received.TrySetResult(results));

            await connection.InvokeAsync("JoinPoll", "abc123");

            var response = await BroadcastAsync(
                new BroadcastRequest(ResultsFor("abc123", 7)),
                RealtimeFactory.TestInternalApiKey);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            var results = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("abc123", results.Code);
            Assert.Equal(7, results.TotalVotes);
        }

        [Fact]
        public async Task A_client_watching_a_different_poll_receives_nothing()
        {
            await using var connection = await ConnectAsync();
            var received = new TaskCompletionSource<PollResultsResponse>();
            connection.On<PollResultsResponse>("ResultsUpdated", results => received.TrySetResult(results));

            await connection.InvokeAsync("JoinPoll", "mine01");

            await BroadcastAsync(
                new BroadcastRequest(ResultsFor("other1", 3)),
                RealtimeFactory.TestInternalApiKey);

            await Assert.ThrowsAsync<TimeoutException>(
                () => received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        }

        [Fact]
        public async Task Leaving_a_poll_stops_the_updates()
        {
            await using var connection = await ConnectAsync();
            var received = new TaskCompletionSource<PollResultsResponse>();
            connection.On<PollResultsResponse>("ResultsUpdated", results => received.TrySetResult(results));

            await connection.InvokeAsync("JoinPoll", "leave1");
            await connection.InvokeAsync("LeavePoll", "leave1");

            await BroadcastAsync(
                new BroadcastRequest(ResultsFor("leave1", 1)),
                RealtimeFactory.TestInternalApiKey);

            await Assert.ThrowsAsync<TimeoutException>(
                () => received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        }

        [Fact]
        public async Task Closing_a_poll_is_delivered_as_its_own_client_method()
        {
            await using var connection = await ConnectAsync();
            var received = new TaskCompletionSource<PollClosedNotification>();
            connection.On<PollClosedNotification>("PollClosed", notification => received.TrySetResult(notification));

            await connection.InvokeAsync("JoinPoll", "close1");

            var response = await PollClosedAsync(
                new PollClosedNotification("close1", PollStatus.Closed, DateTimeOffset.UnixEpoch),
                RealtimeFactory.TestInternalApiKey);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("close1", notification.Code);
            Assert.Equal(PollStatus.Closed, notification.Status);
        }

        [Fact]
        public async Task The_close_endpoint_is_closed_to_callers_without_the_shared_secret()
        {
            var response = await PollClosedAsync(
                new PollClosedNotification("abc123", PollStatus.Closed, DateTimeOffset.UnixEpoch), apiKey: null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task The_broadcast_endpoint_is_closed_to_callers_without_the_shared_secret()
        {
            var response = await BroadcastAsync(
                new BroadcastRequest(ResultsFor("abc123", 1)), apiKey: null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
