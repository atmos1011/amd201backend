namespace PollBuilder.Contracts.Infrastructure
{
    /// <summary>
    /// Settings every service shares, bound from the <c>Service</c> configuration section. On Render
    /// these arrive as environment variables such as <c>Service__AllowedOrigins__0</c>.
    /// </summary>
    public class ServiceOptions
    {
        public const string SectionName = "Service";

        /// <summary>
        /// Browser origins allowed to call this service. Explicit origins are required because SignalR
        /// sends credentials, and CORS forbids combining credentials with a wildcard origin.
        /// </summary>
        public IList<string> AllowedOrigins { get; set; } = [];

        /// <summary>
        /// Public origin of the Vue SPA, used to build share links (e.g. https://myapp.vercel.app).
        /// When blank, share links fall back to the requesting gateway's own origin.
        /// </summary>
        public string ShareBaseUrl { get; set; } = string.Empty;

        /// <summary>Applies EF Core migrations at startup. Render's Docker deploy has no release step.</summary>
        public bool ApplyMigrationsOnStartup { get; set; } = true;
    }

    /// <summary>
    /// Where a service finds its neighbours. Following the pattern taught in the microservices lab,
    /// service-to-service calls go through the API gateway rather than to hardcoded service hosts, so
    /// there is exactly one place to update when a URL changes.
    /// </summary>
    public class ServiceEndpointOptions
    {
        public const string SectionName = "ServiceEndpoints";

        /// <summary>Gateway base URL, e.g. https://pollbuilder-gateway.onrender.com.</summary>
        public string GatewayBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// PollService base URL, used only for the internal callback. The gateway deliberately does not
        /// publish <c>/internal/*</c> routes, so that traffic cannot go through it.
        /// </summary>
        public string PollServiceBaseUrl { get; set; } = string.Empty;

        /// <summary>RealtimeService base URL. Broadcasts go direct: they are internal, not public API.</summary>
        public string RealtimeBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Shared secret sent as <c>X-Internal-Key</c> on service-to-service calls, so the internal
        /// broadcast endpoint cannot be driven from the public internet.
        /// </summary>
        public string InternalApiKey { get; set; } = string.Empty;
    }
}
