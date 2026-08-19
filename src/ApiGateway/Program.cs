using Microsoft.Extensions.Options;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Gateway.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ---------------------------------------------------------------------------
// Route table
// ---------------------------------------------------------------------------
// ocelot.json declares the public API surface; the overrides below point each route at the real
// service URL for this environment (docker-compose names locally, Render hostnames in production).
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.Configure<DownstreamOptions>(builder.Configuration.GetSection(DownstreamOptions.SectionName));
var downstream = builder.Configuration.GetSection(DownstreamOptions.SectionName).Get<DownstreamOptions>()
    ?? new DownstreamOptions();

builder.Configuration.AddInMemoryCollection(OcelotHostOverrides.Build(builder.Configuration, downstream));

builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

// The SignalR route is a WebSocket upgrade, so the gateway has to speak WebSockets itself.
app.UseWebSockets();

// Must run before Ocelot: the browser's preflight for a cross-origin API call is answered here, not
// by the downstream service.
app.UseCors(ServiceDefaults.CorsPolicy);

// Ocelot terminates the pipeline for every request it recognises, so the gateway's own endpoints are
// handled by this middleware ahead of it rather than by endpoint routing behind it.
app.Use(async (context, next) =>
{
    switch (context.Request.Path.Value)
    {
        case "/":
            await context.Response.WriteAsync("Poll & Survey Builder API gateway is running.");
            return;
        case "/health":
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""{"status":"Healthy","poll":"{{downstream.PollService}}","voting":"{{downstream.VotingService}}","realtime":"{{downstream.RealtimeService}}"}""");
            return;
        default:
            await next(context);
            return;
    }
});

await app.UseOcelot();

await app.RunAsync();
