using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Polls.Data;
using PollBuilder.Polls.Repo;
using PollBuilder.Polls.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ---------------------------------------------------------------------------
// Persistence - this service owns the "polls" schema and nothing else touches it.
// ---------------------------------------------------------------------------
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration)
    ?? throw new InvalidOperationException(
        "No PostgreSQL connection string. Set ConnectionStrings__Postgres (or DATABASE_URL) in the " +
        "environment. See README.md for the Neon setup steps.");

builder.Services.AddDbContext<PollDbContext>(options =>
    // Keep the migrations ledger inside this service's own schema. Both services share one Neon
    // database to stay on the free tier, and a shared public.__EFMigrationsHistory would mean two
    // independently deployable services writing to one table - exactly the coupling the schema
    // split exists to avoid.
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, PollDbContext.Schema)));
builder.Services.AddHealthChecks().AddDbContextCheck<PollDbContext>("database");

builder.Services.AddScoped<IPollRepo, PollRepo>();
builder.Services.AddScoped<IPollService, PollService>();
builder.Services.AddSingleton<IPollCodeGenerator, PollCodeGenerator>();

// Close notifications go straight to RealtimeService: they are internal traffic, and the gateway
// deliberately publishes no /internal routes.
var endpoints = builder.Configuration.GetSection(ServiceEndpointOptions.SectionName).Get<ServiceEndpointOptions>()
    ?? new ServiceEndpointOptions();

builder.Services.AddHttpClient<IRealtimeNotifier, RealtimeNotifierClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(endpoints.RealtimeBaseUrl))
    {
        client.BaseAddress = new Uri(endpoints.RealtimeBaseUrl.TrimEnd('/') + "/");
    }

    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddControllers().AddSharedJsonOptions();
builder.Services.AddOpenApi();

var app = builder.Build();

var serviceOptions = app.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<ServiceOptions>>().Value;

if (serviceOptions.ApplyMigrationsOnStartup)
{
    // Render deploys a container with no separate release step, so the schema is brought up to date
    // here rather than by a manual `dotnet ef database update` against production.
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Applying migrations to {Database}", DatabaseConnectionString.Describe(connectionString));
    await scope.ServiceProvider.GetRequiredService<PollDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Render terminates TLS at its edge and forwards plain HTTP, so redirecting here would loop.
app.UseCors(ServiceDefaults.CorsPolicy);

app.MapControllers();
app.MapHealthChecks("/health");
app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Poll Service API"));
app.MapGet("/", () => "PollService is running.");

await app.RunAsync();

/// <summary>Exposed so the integration tests can boot the real service with WebApplicationFactory.</summary>
public partial class Program;
