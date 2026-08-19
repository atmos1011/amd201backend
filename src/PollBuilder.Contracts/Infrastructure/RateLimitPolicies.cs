namespace PollBuilder.Contracts.Infrastructure
{
    /// <summary>Names of the rate-limiting policies registered in each service's startup.</summary>
    public static class RateLimitPolicies
    {
        /// <summary>
        /// Throttles the vote endpoint per client IP. Duplicate voting is already blocked by the unique
        /// index; this is about stopping someone hammering a free-tier instance with junk requests.
        /// </summary>
        public const string Vote = "vote";
    }
}
