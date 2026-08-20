using ResultManage.Hubs;
using ResultManage.Services;

var builder = WebApplication.CreateBuilder(args);

// This service has no database of its own. It asks PollManage and VoteManage for
// what it needs, then puts the two answers together.

// -----------------------------------------------------------
var gatewayBaseUrl = builder.Configuration["ServiceEndpoints:ApiGatewayBaseUrl"];
if (string.IsNullOrEmpty(gatewayBaseUrl))
{
    throw new InvalidOperationException("API Gateway base URL is not configured in ServiceEndpoints:ApiGatewayBaseUrl");
}

builder.Services.AddHttpClient<PollService>(client =>
    client.BaseAddress = new Uri(gatewayBaseUrl.EndsWith("/") ? gatewayBaseUrl : gatewayBaseUrl + "/"));
builder.Services.AddHttpClient<VoteService>(client =>
    client.BaseAddress = new Uri(gatewayBaseUrl.EndsWith("/") ? gatewayBaseUrl : gatewayBaseUrl + "/"));

// Combines the answers from the two services above
builder.Services.AddScoped<ResultService>();
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

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowVue");

// Render handles HTTPS at its edge, so redirecting here would cause a loop.
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHub<PollHub>("/hubs/poll");

app.MapGet("/", () => "ResultManage is running!");

app.Run();
