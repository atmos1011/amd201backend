namespace PollBuilder.Polls.Services
{
    /// <summary>Produces the short, shareable poll codes that appear in links like /poll/7fGh2a.</summary>
    public interface IPollCodeGenerator
    {
        string Next();
    }
}
