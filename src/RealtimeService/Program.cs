using System.Text.Json.Serialization;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Realtime.Hubs;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers().AddSharedJsonOptions();
builder.Services.AddOpenApi();

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        // Match the REST payloads: "Open"/"Closed" rather than 0/1, so the SPA can hand a SignalR
        // message to the same renderer it uses for the initial fetch.
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Must run before the hub is mapped: the SignalR handshake is a cross-origin credentialed request.
app.UseCors(ServiceDefaults.CorsPolicy);

app.MapControllers();
app.MapHub<PollHub>("/hubs/poll");
app.MapHealthChecks("/health");
app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Realtime Service API"));
app.MapGet("/", () => "RealtimeService is running.");

await app.RunAsync();

/// <summary>Exposed so integration tests can boot the real service with WebApplicationFactory.</summary>
public partial class Program;
