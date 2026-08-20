using Microsoft.EntityFrameworkCore;
using VoteManage.Data;
using VoteManage.Repo;
using VoteManage.Services;

var builder = WebApplication.CreateBuilder(args);

// Neon connection string. It lives in appsettings.json for local work, and on Render it is
// overridden by the ConnectionStrings__myContext environment variable.
var connectionString = builder.Configuration.GetConnectionString("myContext");

// appsettings.json ships this key empty on purpose, so the real value can come from an
// environment variable. Checking for null only is not enough: an empty string is not null,
// and it would get all the way to Npgsql before failing with a much less obvious message.
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'myContext' is empty. Set the ConnectionStrings__myContext "
        + "environment variable (two underscores) to your Neon connection string.");
}

builder.Services.AddDbContext<myContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IVoteRepo, VoteRepo>();

// -----------------------------------------------------------
// This service asks PollManage about polls and ResultManage to publish results,
// and it goes through the gateway to do both.
var gatewayBaseUrl = builder.Configuration["ServiceEndpoints:ApiGatewayBaseUrl"];
if (string.IsNullOrEmpty(gatewayBaseUrl))
{
    throw new InvalidOperationException("API Gateway base URL is not configured in ServiceEndpoints:ApiGatewayBaseUrl");
}

builder.Services.AddHttpClient<PollService>(client =>
    client.BaseAddress = new Uri(gatewayBaseUrl.EndsWith("/") ? gatewayBaseUrl : gatewayBaseUrl + "/"));
builder.Services.AddHttpClient<ResultService>(client =>
    client.BaseAddress = new Uri(gatewayBaseUrl.EndsWith("/") ? gatewayBaseUrl : gatewayBaseUrl + "/"));
// -----------------------------------------------------------

builder.Services.AddControllers();

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

app.MapGet("/", () => "VoteManage is running!");

app.Run();
