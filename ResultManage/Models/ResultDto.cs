namespace ResultManage.Models
{
    // What PollManage (service A) sends back when we ask about a poll
    public class PollDetailsDto
    {
        public string Code { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<PollOptionDetailsDto> Options { get; set; } = new List<PollOptionDetailsDto>();
    }

    public class PollOptionDetailsDto
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    // What VoteManage (service B) sends back when we ask for the counts
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

    // The finished results: option text from A joined to vote counts from B.
    // This is also the exact object pushed over SignalR.
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
}
