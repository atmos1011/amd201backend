using System.ComponentModel.DataAnnotations;
using PollBuilder.Contracts.Polls;

namespace PollBuilder.Contracts.Voting
{
    /// <summary>Body of <c>POST /api/polls/{code}/vote</c>.</summary>
    public class VoteRequest
    {
        /// <summary>Zero-based index of the chosen option.</summary>
        [Range(0, PollValidation.MaxOptions - 1)]
        public int OptionIndex { get; set; }
    }

    /// <summary>Vote tally for a single option.</summary>
    public record OptionResultResponse(int Index, string Text, int Votes, double Percentage);

    /// <summary>
    /// Live results payload. This is also the exact shape broadcast over SignalR as
    /// <c>ResultsUpdated</c>, so the SPA can reuse one renderer for the initial fetch and for realtime
    /// updates.
    /// </summary>
    public record PollResultsResponse(
        string Code,
        string Question,
        PollStatus Status,
        bool AcceptsVotes,
        int TotalVotes,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<OptionResultResponse> Options);

    /// <summary>
    /// Response to a successful vote. <see cref="VoterToken"/> is echoed so a first-time respondent can
    /// persist it and be recognised on later requests.
    /// </summary>
    public record VoteResponse(string VoterToken, PollResultsResponse Results);
}
