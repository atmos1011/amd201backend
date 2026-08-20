using Microsoft.EntityFrameworkCore;
using PollManage.Data;
using PollManage.Repo;

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

builder.Services.AddScoped<IPollRepo, PollRepo>();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The Vue app and the gateway both call this service from another origin.
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

app.MapGet("/", () => "PollManage is running!");

app.Run();
