namespace PollBuilder.Polls.Models
{
    /// <summary>
    /// One answer choice belonging to a poll. Options are a separate table rather than a serialised
    /// column on <see cref="Poll"/>, so they can be indexed, validated and replaced individually.
    /// </summary>
    public class PollOption
    {
        public int Id { get; set; }

        public int PollId { get; set; }

        public Poll? Poll { get; set; }

        /// <summary>Zero-based position of this option within its poll (0-5).</summary>
        public int OptionIndex { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}
