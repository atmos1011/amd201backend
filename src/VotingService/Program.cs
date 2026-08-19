using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Voting.Data;
using PollBuilder.Voting.Repo;
using PollBuilder.Voting.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ---------------------------------------------------------------------------
// Persistence - this service owns the "voting" schema and nothing else touches it.
// ---------------------------------------------------------------------------
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration)
    ?? throw new InvalidOperationException(
        "No PostgreSQL connection string. Set ConnectionStrings__Postgres (or DATABASE_URL) in the " +
        "environment. See README.md for the Neon setup steps.");

builder.Services.AddDbContext<VotingDbContext>(options =>
    // See the matching comment in PollService: each service keeps its own migrations ledger inside
    // the schema it owns, so the two never write to the same table.
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, VotingDbContext.Schema)));
builder.Services.AddHealthChecks().AddDbContextCheck<VotingDbContext>("database");

builder.Services.AddScoped<IVoteRepo, VoteRepo>();
builder.Services.AddScoped<IVotingService, VotingService>();

// ---------------------------------------------------------------------------
// Downstream services
// ---------------------------------------------------------------------------
var endpoints = builder.Configuration.GetSection(ServiceEndpointOptions.SectionName).Get<ServiceEndpointOptions>()
    ?? new ServiceEndpointOptions();

if (string.IsNullOrWhiteSpace(endpoints.GatewayBaseUrl))
{
    throw new InvalidOperationException(
        "ServiceEndpoints__GatewayBaseUrl is not configured; VotingService cannot reach PollService.");
}

// Poll lookups go through the gateway, as taught in the microservices lab. Broadcasts go direct to
// RealtimeService because they are internal traffic that the gateway deliberately does not publish.
builder.Services.AddHttpClient<IPollCatalog, PollCatalogClient>(client =>
    {
        client.BaseAddress = new Uri(EnsureTrailingSlash(endpoints.GatewayBaseUrl));
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IPollNotifier, RealtimeNotifierClient>(client =>
{
    client.BaseAddress = new Uri(EnsureTrailingSlash(
        string.IsNullOrWhiteSpace(endpoints.RealtimeBaseUrl) ? endpoints.GatewayBaseUrl : endpoints.RealtimeBaseUrl));
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddControllers().AddSharedJsonOptions();
builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Vote, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

if (app.Services.GetRequiredService<IOptions<ServiceOptions>>().Value.ApplyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Applying migrations to {Database}", DatabaseConnectionString.Describe(connectionString));
    await scope.ServiceProvider.GetRequiredService<VotingDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(ServiceDefaults.CorsPolicy);
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Voting Service API"));
app.MapGet("/", () => "VotingService is running.");

await app.RunAsync();

static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";

/// <summary>Exposed so the integration tests can boot the real service with WebApplicationFactory.</summary>
public partial class Program;
