using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PollBuilder.Contracts.Errors;

namespace PollBuilder.Contracts.Infrastructure
{
    /// <summary>
    /// Startup wiring shared by all four services: CORS, ProblemDetails, JSON conventions and health
    /// checks. Without this each service would repeat the same 40 lines and they would drift apart.
    /// </summary>
    public static class ServiceDefaults
    {
        /// <summary>CORS policy name used by every service.</summary>
        public const string CorsPolicy = "AllowSpa";

        /// <summary>Header carrying the shared secret on internal service-to-service calls.</summary>
        public const string InternalApiKeyHeader = "X-Internal-Key";

        /// <summary>Header the SPA uses to identify a returning respondent.</summary>
        public const string VoterTokenHeader = "X-Voter-Token";

        /// <summary>Header proving the caller created the poll.</summary>
        public const string CreatorTokenHeader = "X-Creator-Token";

        /// <summary>Binds shared options and registers CORS, ProblemDetails and the exception handler.</summary>
        public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.Configure<ServiceOptions>(builder.Configuration.GetSection(ServiceOptions.SectionName));
            builder.Services.Configure<ServiceEndpointOptions>(
                builder.Configuration.GetSection(ServiceEndpointOptions.SectionName));

            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<ServiceExceptionHandler>();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddHealthChecks();

            var options = builder.Configuration.GetSection(ServiceOptions.SectionName).Get<ServiceOptions>()
                ?? new ServiceOptions();

            builder.Services.AddCors(cors => cors.AddPolicy(CorsPolicy, policy => Configure(policy, options)));

            return builder;
        }

        /// <summary>JSON settings shared by every service: enums as strings, camelCase properties.</summary>
        public static IMvcBuilder AddSharedJsonOptions(this IMvcBuilder mvcBuilder)
        {
            ArgumentNullException.ThrowIfNull(mvcBuilder);

            return mvcBuilder.AddJsonOptions(json =>
                // Statuses travel as "Open"/"Closed" rather than 0/1 so the SPA and docs stay readable.
                json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        }

        private static void Configure(CorsPolicyBuilder policy, ServiceOptions options)
        {
            var origins = options.AllowedOrigins.Count > 0
                ? [.. options.AllowedOrigins]
                : new[] { "http://localhost:5173", "http://localhost:4173" };

            policy.WithOrigins(origins)
                  // Vercel gives every preview deployment its own subdomain, so match those too.
                  .SetIsOriginAllowed(origin =>
                      origins.Contains(origin, StringComparer.OrdinalIgnoreCase)
                      || (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                          && uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)))
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  // SignalR's WebSocket handshake sends credentials.
                  .AllowCredentials()
                  // Let the browser read the voter token the server issues on a first vote.
                  .WithExposedHeaders(VoterTokenHeader);
        }
    }
}
