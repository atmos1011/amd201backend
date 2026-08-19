using Microsoft.EntityFrameworkCore;
using VoteManage.Data;
using VoteManage.Hubs;
using VoteManage.Repo;
using VoteManage.Services;

var builder = WebApplication.CreateBuilder(args);

// Neon connection string. It lives in appsettings.json for local work, and on Render it is
// overridden by the ConnectionStrings__myContext environment variable.
var connectionString = builder.Configuration.GetConnectionString("myContext")
    ?? throw new InvalidOperationException("Connection string 'myContext' not found.");

builder.Services.AddDbContext<myContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IVoteRepo, VoteRepo>();

// -----------------------------------------------------------
// This service asks PollManage about polls, and it goes through the gateway to do it.
var gatewayBaseUrl = builder.Configuration["ServiceEndpoints:ApiGatewayBaseUrl"];
if (string.IsNullOrEmpty(gatewayBaseUrl))
{
    throw new InvalidOperationException("API Gateway base URL is not configured in ServiceEndpoints:ApiGatewayBaseUrl");
}

builder.Services.AddHttpClient<PollService>(client =>
    client.BaseAddress = new Uri(gatewayBaseUrl.EndsWith("/") ? gatewayBaseUrl : gatewayBaseUrl + "/"));
// -----------------------------------------------------------

builder.Services.AddControllers();

// The live results page keeps a WebSocket open to this service.
builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVue", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // SignalR sends credentials when it connects, so this is needed and it is why
            // AllowAnyOrigin cannot be used here.
            .AllowCredentials();
    });
});

var app = builder.Build();

// Render deploys the container with no separate step for migrations, so the tables are
// created here on startup instead of running update-database by hand.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<myContext>().Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowVue");

// Render handles HTTPS at its edge, so redirecting here would cause a loop.
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHub<PollHub>("/hubs/poll");

app.MapGet("/", () => "VoteManage is running!");

app.Run();
