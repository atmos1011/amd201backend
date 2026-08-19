using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PollBuilder.Polls.Data;

namespace PollService.Tests.Integration
{
    /// <summary>
    /// Boots the real PollService application - real routing, model binding, filters and ProblemDetails
    /// - with PostgreSQL swapped for an in-memory SQLite database.
    /// </summary>
    /// <remarks>
    /// SQLite rather than EF Core's InMemory provider on purpose: InMemory silently ignores unique
    /// indexes, so a duplicate-code test would pass without the constraint ever being exercised. SQLite
    /// enforces them, which is the behaviour under test.
    /// </remarks>
    public class PollApiFactory : WebApplicationFactory<Program>
    {
        /// <summary>Shared secret the tests present on internal service-to-service endpoints.</summary>
        public const string TestInternalApiKey = "test-internal-key";

        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        public PollApiFactory()
        {
            // Program.cs reads configuration during Main, before WebApplicationFactory's deferred
            // ConfigureAppConfiguration callbacks would run, so these have to be environment variables.
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__Postgres", "Host=localhost;Database=unused;Username=test;Password=test");
            Environment.SetEnvironmentVariable("Service__ApplyMigrationsOnStartup", "false");
            Environment.SetEnvironmentVariable("Service__ShareBaseUrl", "https://spa.test");
            Environment.SetEnvironmentVariable("Service__AllowedOrigins__0", "https://spa.test");
            Environment.SetEnvironmentVariable("ServiceEndpoints__InternalApiKey", TestInternalApiKey);

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

                services.AddDbContext<PollDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<PollDbContext>().Database.EnsureCreated();

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
}
