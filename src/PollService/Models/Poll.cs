using PollBuilder.Contracts.Polls;

namespace PollBuilder.Polls.Models
{
    /// <summary>
    /// A multiple-choice question. Identified publicly by <see cref="Code"/>, never by <see cref="Id"/>,
    /// so poll ids cannot be enumerated from a share link.
    /// </summary>
    public class Poll
    {
        public int Id { get; set; }

        /// <summary>Short, URL-safe public identifier, e.g. "7fGh2a".</summary>
        public string Code { get; set; } = string.Empty;

        public string Question { get; set; } = string.Empty;

        public PollStatus Status { get; set; } = PollStatus.Open;

        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Optional auto-close time. A poll past this instant stops accepting votes.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        public DateTimeOffset? ClosedAt { get; set; }

        /// <summary>
        /// SHA-256 of the creator token. The raw token is returned once at creation and never stored,
        /// so a leaked database row cannot be used to close or edit someone else's poll.
        /// </summary>
        public string CreatorTokenHash { get; set; } = string.Empty;

        /// <summary>
        /// Set by VotingService the first time a vote lands, via the internal callback. PollService owns
        /// no vote data of its own, so this flag is how it knows an edit would rewrite history.
        /// </summary>
        public bool HasVotes { get; set; }

        public ICollection<PollOption> Options { get; set; } = [];

        /// <summary>True when an expiry was set and has already passed.</summary>
        public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;

        /// <summary>A poll accepts votes only while it is open and unexpired.</summary>
        public bool AcceptsVotes(DateTimeOffset now) => Status == PollStatus.Open && !IsExpired(now);
    }
}
