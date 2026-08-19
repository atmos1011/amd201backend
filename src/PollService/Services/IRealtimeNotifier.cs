namespace PollBuilder.Polls.Services
{
    /// <summary>
    /// Tells RealtimeService that a poll has closed, so watchers see the vote form disable itself
    /// without refreshing. Abstracted away from HTTP so the close rules stay unit-testable.
    /// </summary>
    public interface IRealtimeNotifier
    {
        Task PollClosedAsync(string code, DateTimeOffset? closedAt, CancellationToken cancellationToken = default);
    }
}
