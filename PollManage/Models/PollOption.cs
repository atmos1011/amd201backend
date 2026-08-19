using System.ComponentModel.DataAnnotations;

namespace PollManage.Models
{
    public class PollOption
    {
        public int Id { get; set; }

        public int PollId { get; set; }

        public Poll? Poll { get; set; }

        // 0 to 5. This is the number the voter sends when they vote.
        public int OptionIndex { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;
    }
}
