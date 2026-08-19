using System.Globalization;

namespace PollBuilder.Gateway.Configuration
{
    /// <summary>
    /// Rewrites the downstream hosts declared in <c>ocelot.json</c> from configuration.
    /// </summary>
    /// <remarks>
    /// ocelot.json is checked in with docker-compose service names, so the gateway works locally with no
    /// extra setup. Ocelot has no placeholder substitution of its own, and overriding a dozen
    /// <c>Routes__0__DownstreamHostAndPorts__0__Host</c> environment variables by hand on Render would be
    /// error-prone, so this maps one URL per service onto every route that points at it.
    /// </remarks>
    public static class OcelotHostOverrides
    {
        /// <summary>
        /// Produces the configuration keys that redirect each route to its real deployed host. Returns an
        /// empty map when nothing is configured, leaving ocelot.json untouched.
        /// </summary>
        public static IReadOnlyDictionary<string, string?> Build(IConfiguration configuration, DownstreamOptions options)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(options);

            var targets = options.ByPlaceholderHost();
            var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var route in configuration.GetSection("Routes").GetChildren())
            {
                var placeholder = route["DownstreamHostAndPorts:0:Host"];
                if (placeholder is null
                    || !targets.TryGetValue(placeholder, out var url)
                    || string.IsNullOrWhiteSpace(url)
                    || !Uri.TryCreate(url, UriKind.Absolute, out var target))
                {
                    continue;
                }

                var isWebSocket = string.Equals(route["DownstreamScheme"], "ws", StringComparison.OrdinalIgnoreCase);
                var secure = target.Scheme is "https" or "wss";

                overrides[$"Routes:{route.Key}:DownstreamHostAndPorts:0:Host"] = target.Host;
                overrides[$"Routes:{route.Key}:DownstreamHostAndPorts:0:Port"] =
                    target.Port.ToString(CultureInfo.InvariantCulture);
                overrides[$"Routes:{route.Key}:DownstreamScheme"] = (isWebSocket, secure) switch
                {
                    (true, true) => "wss",
                    (true, false) => "ws",
                    (false, true) => "https",
                    (false, false) => "http"
                };
            }

            return overrides;
        }
    }
}
