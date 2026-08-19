namespace PollBuilder.Gateway.Configuration
{
    /// <summary>
    /// Where the gateway can reach each service, bound from the <c>Downstream</c> section. On Render
    /// these arrive as <c>Downstream__PollService</c> and friends, holding full URLs such as
    /// <c>https://pollbuilder-poll.onrender.com</c>.
    /// </summary>
    public class DownstreamOptions
    {
        public const string SectionName = "Downstream";

        public string PollService { get; set; } = string.Empty;

        public string VotingService { get; set; } = string.Empty;

        public string RealtimeService { get; set; } = string.Empty;

        /// <summary>Placeholder host in ocelot.json mapped to the URL for each service.</summary>
        public IReadOnlyDictionary<string, string> ByPlaceholderHost() => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["poll-service"] = PollService,
            ["voting-service"] = VotingService,
            ["realtime-service"] = RealtimeService
        };
    }
}
