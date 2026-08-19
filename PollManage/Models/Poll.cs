using System.ComponentModel.DataAnnotations;

namespace PollManage.Models
{
    public class Poll
    {
        public int Id { get; set; }

        // The short code that goes in the share link, e.g. /poll/7fGh2a
        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Question { get; set; } = string.Empty;

        // "Open" or "Closed". A string keeps it readable when you look at the table in Neon.
        [Required]
        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        // Given to the creator once when the poll is made. Only someone holding this
        // can edit or close the poll.
        [Required]
        public string CreatorToken { get; set; } = string.Empty;

        // Set to true by VoteManage after the first vote, so the question and options
        // can no longer be changed underneath people who already voted.
        public bool HasVotes { get; set; }

        public List<PollOption> Options { get; set; } = new List<PollOption>();
    }
}
