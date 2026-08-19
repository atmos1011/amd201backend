namespace PollBuilder.Voting.Models
{
    /// <summary>
    /// A single respondent's choice. The pair (<see cref="PollCode"/>, <see cref="VoterToken"/>) carries
    /// a unique index, so "one vote per respondent" is enforced by the database rather than only by code.
    /// </summary>
    /// <remarks>
    /// Votes reference a poll by its public <see cref="PollCode"/>, not by a foreign key: the Polls table
    /// lives in another service's schema, and a cross-service FK would couple the two databases together
    /// and defeat the point of separating them.
    /// </remarks>
    public class Vote
    {
        public int Id { get; set; }

        public string PollCode { get; set; } = string.Empty;

        /// <summary>Index of the chosen option, as defined by PollService.</summary>
        public int OptionIndex { get; set; }

        /// <summary>Opaque per-browser identity issued by this service; no login required.</summary>
        public string VoterToken { get; set; } = string.Empty;

        public DateTimeOffset VotedAt { get; set; }
    }
}
