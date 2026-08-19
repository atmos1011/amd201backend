using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Configuration.AddJsonFile(
    "ocelot.json",
    optional: false,
    reloadOnChange: true
    );

// On Render, ASPNETCORE_ENVIRONMENT is Production, so ocelot.Production.json is loaded on top
// of ocelot.json and swaps the localhost addresses for the deployed ones.
builder.Configuration.AddJsonFile(
    $"ocelot.{builder.Environment.EnvironmentName}.json",
    optional: true,
    reloadOnChange: true
    );

builder.Services.AddOcelot(builder.Configuration);

// The Vue app calls the gateway from another origin, so CORS is answered here.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVue", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// The SignalR route is a WebSocket, so the gateway has to speak WebSockets to pass it through.
app.UseWebSockets();

app.UseCors("AllowVue");

// Ocelot answers every request it recognises, so the gateway's own two pages are handled
// before it in the pipeline.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        await context.Response.WriteAsync("Poll Builder API Gateway is running!");
        return;
    }

    if (context.Request.Path == "/health")
    {
        await context.Response.WriteAsync("Healthy");
        return;
    }

    await next(context);
});

// Use Ocelot Middleware - This is crucial!
await app.UseOcelot();

app.Run();
