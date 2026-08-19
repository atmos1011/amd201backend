using System.ComponentModel.DataAnnotations;

namespace PollManage.Models
{
    // Dto for reading a poll
    public class PollDto
    {
        public string Code { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public bool HasVotes { get; set; }
        public List<PollOptionDto> Options { get; set; } = new List<PollOptionDto>();
    }

    public class PollOptionDto
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    // Returned only once, when the poll is created, because it carries the creator token
    public class CreatedPollDto : PollDto
    {
        public string CreatorToken { get; set; } = string.Empty;
        public string ShareUrl { get; set; } = string.Empty;
    }

    public class CreatePoll
    {
        [Required]
        [StringLength(300, MinimumLength = 3)]
        public string Question { get; set; } = string.Empty;

        [Required]
        [MinLength(2)]
        [MaxLength(6)]
        public List<string> Options { get; set; } = new List<string>();
    }

    public class UpdatePoll
    {
        [Required]
        [StringLength(300, MinimumLength = 3)]
        public string Question { get; set; } = string.Empty;

        [Required]
        [MinLength(2)]
        [MaxLength(6)]
        public List<string> Options { get; set; } = new List<string>();
    }

    // For PATCH: every field is optional, only the ones sent are changed
    public class PatchPoll
    {
        [StringLength(300, MinimumLength = 3)]
        public string? Question { get; set; }

        public List<string>? Options { get; set; }

        // "Open" or "Closed"
        public string? Status { get; set; }
    }
}
