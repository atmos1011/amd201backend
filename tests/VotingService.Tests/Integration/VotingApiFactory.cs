using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PollBuilder.Contracts.Polls;
using PollBuilder.Voting.Data;
using PollBuilder.Voting.Repo;
using PollBuilder.Voting.Services;

namespace VotingService.Tests.Integration
{
    /// <summary>
    /// Boots the real VotingService with SQLite in place of PostgreSQL and a controllable stand-in for
    /// PollService, so the vote endpoints can be tested over real HTTP without a second service running.
    /// </summary>
    /// <remarks>
    /// SQLite rather than EF Core's InMemory provider on purpose: InMemory ignores unique indexes, and
    /// the unique (PollCode, VoterToken) index is exactly what stops a respondent voting twice.
    /// </remarks>
    public class VotingApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        /// <summary>The poll every test votes on. Mutate this to close the poll or change its options.</summary>
        public TestPollCatalog Polls { get; } = new();

        public VotingApiFactory()
        {
            // Program.cs reads configuration during Main, before deferred ConfigureAppConfiguration
            // callbacks would run, so these have to be environment variables.
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__Postgres", "Host=localhost;Database=unused;Username=test;Password=test");
            Environment.SetEnvironmentVariable("Service__ApplyMigrationsOnStartup", "false");
            Environment.SetEnvironmentVariable("ServiceEndpoints__GatewayBaseUrl", "http://gateway.test");
            Environment.SetEnvironmentVariable("ServiceEndpoints__RealtimeBaseUrl", "http://realtime.test");
            Environment.SetEnvironmentVariable("ServiceEndpoints__InternalApiKey", "test-internal-key");

            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                             .Where(d => d.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal) == true)
                             .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<VotingDbContext>(options => options.UseSqlite(_connection));

                // Replace the two HTTP clients so nothing reaches out over the network during a test.
                services.RemoveAll<IPollCatalog>();
                services.AddSingleton<IPollCatalog>(Polls);
                services.RemoveAll<IPollNotifier>();
                services.AddSingleton<IPollNotifier, NoOpNotifier>();
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<VotingDbContext>().Database.EnsureCreated();

            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }

    /// <summary>Mutable stand-in for PollService, so a test can close the poll mid-scenario.</summary>
    public sealed class TestPollCatalog : IPollCatalog
    {
        private readonly Dictionary<string, PollDto> _polls = new(StringComparer.Ordinal);

        public void Set(PollDto poll) => _polls[poll.Code] = poll;

        public void Close(string code)
        {
            if (_polls.TryGetValue(code, out var poll))
            {
                _polls[code] = poll with { Status = PollStatus.Closed, AcceptsVotes = false };
            }
        }

        public Task<PollDto?> GetPollAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(_polls.GetValueOrDefault(code));

        public Task NotifyVotesRecordedAsync(string code, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    internal sealed class NoOpNotifier : IPollNotifier
    {
        public Task ResultsUpdatedAsync(
            PollBuilder.Contracts.Voting.PollResultsResponse results, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
