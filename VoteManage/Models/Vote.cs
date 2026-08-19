using System.ComponentModel.DataAnnotations;

namespace VoteManage.Models
{
    public class Vote
    {
        public int Id { get; set; }

        // The poll this vote belongs to. It is the poll's Code and not a foreign key,
        // because the Polls table belongs to PollManage and lives in its own schema.
        [Required]
        public string PollCode { get; set; } = string.Empty;

        public int OptionIndex { get; set; }

        // Identifies one browser. The server makes it up on the first vote, there is no login.
        [Required]
        public string VoterToken { get; set; } = string.Empty;

        public DateTime VotedAt { get; set; }
    }
}
