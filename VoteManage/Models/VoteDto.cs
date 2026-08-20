using System.ComponentModel.DataAnnotations;

namespace VoteManage.Models
{
    public class CreateVote
    {
        [Range(0, 5)]
        public int OptionIndex { get; set; }
    }

    // What the voter gets back after voting
    public class VoteResultDto
    {
        public string VoterToken { get; set; } = string.Empty;
        public PollResultDto? Results { get; set; }
    }

    // The counts this service owns, handed to ResultManage so it can add the option text
    public class VoteCountsDto
    {
        public int TotalVotes { get; set; }
        public List<OptionCountDto> Counts { get; set; } = new List<OptionCountDto>();
    }

    public class OptionCountDto
    {
        public int OptionIndex { get; set; }
        public int Votes { get; set; }
    }

    // What ResultManage sends back after it has built the results
    public class PollResultDto
    {
        public string Code { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalVotes { get; set; }
        public List<OptionResultDto> Options { get; set; } = new List<OptionResultDto>();
    }

    public class OptionResultDto
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Votes { get; set; }
        public double Percentage { get; set; }
    }

    // What PollManage sends back when we ask it about a poll
    public class PollDetailsDto
    {
        public string Code { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool HasVotes { get; set; }
        public List<PollOptionDetailsDto> Options { get; set; } = new List<PollOptionDetailsDto>();
    }

    public class PollOptionDetailsDto
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
