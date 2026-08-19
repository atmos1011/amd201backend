using PollBuilder.Contracts.Polls;

namespace PollBuilder.Polls.Services
{
    /// <summary>
    /// All poll business rules. Controllers do HTTP only; every rule about who may do what, and when,
    /// lives behind this interface.
    /// </summary>
    public interface IPollService
    {
        Task<CreatedPollResponse> CreateAsync(
            CreatePollRequest request, string shareBaseUrl, CancellationToken cancellationToken = default);

        Task<PollDto> GetAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>Full replacement (PUT). Requires the creator token.</summary>
        Task<PollDto> ReplaceAsync(
            string code, UpdatePollRequest request, string? creatorToken, CancellationToken cancellationToken = default);

        /// <summary>Partial update (PATCH), including closing or reopening. Requires the creator token.</summary>
        Task<PollDto> PatchAsync(
            string code, PatchPollRequest request, string? creatorToken, CancellationToken cancellationToken = default);

        /// <summary>Convenience shortcut for PATCH { status: "Closed" }. Requires the creator token.</summary>
        Task<PollDto> CloseAsync(string code, string? creatorToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Internal callback from VotingService the first time a vote is recorded. Flips the flag that
        /// stops the creator editing the question or options out from under existing voters.
        /// </summary>
        Task MarkHasVotesAsync(string code, CancellationToken cancellationToken = default);
    }
}
