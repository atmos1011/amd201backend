using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Voting;
using PollBuilder.Voting.Repo;
using PollBuilder.Voting.Services;

namespace PollBuilder.Voting.Controllers
{
    /// <summary>
    /// Voting and results endpoints. The gateway publishes these as <c>/api/polls/{code}/vote</c> and
    /// <c>/api/polls/{code}/results</c>, which are the paths the assignment brief specifies; internally
    /// they live under <c>/api/votes</c> because this service owns votes, not polls.
    /// </summary>
    [ApiController]
    [Route("api/votes")]
    [Produces("application/json")]
    public class VotesController : ControllerBase
    {
        private readonly IVotingService _votingService;

        public VotesController(IVotingService votingService)
        {
            _votingService = votingService;
        }

        /// <summary>Casts one vote. A respondent without a token is issued one in the response.</summary>
        [HttpPost("{code}")]
        [EnableRateLimiting(RateLimitPolicies.Vote)]
        [ProducesResponseType(typeof(VoteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<VoteResponse>> Vote(
            string code,
            [FromBody] VoteRequest request,
            [FromHeader(Name = ServiceDefaults.VoterTokenHeader)] string? voterToken,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = await _votingService.VoteAsync(code, request.OptionIndex, voterToken, cancellationToken);

            // Echo the token in a header too, so a client can pick it up without parsing the body.
            Response.Headers[ServiceDefaults.VoterTokenHeader] = result.VoterToken;
            return Ok(result);
        }

        /// <summary>Current tallies for a poll.</summary>
        [HttpGet("{code}/results")]
        [ProducesResponseType(typeof(PollResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PollResultsResponse>> GetResults(
            string code, CancellationToken cancellationToken) =>
            Ok(await _votingService.GetResultsAsync(code, cancellationToken));

        /// <summary>Whether this respondent has already voted, so the SPA can skip straight to results.</summary>
        [HttpGet("{code}/me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVoterState(
            string code,
            [FromHeader(Name = ServiceDefaults.VoterTokenHeader)] string? voterToken,
            CancellationToken cancellationToken) =>
            Ok(new { hasVoted = await _votingService.HasVotedAsync(code, voterToken, cancellationToken) });

        /// <summary>Downloads the results as CSV, for taking the data into a spreadsheet.</summary>
        [HttpGet("{code}/results.csv")]
        [Produces("text/csv")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetResultsCsv(string code, CancellationToken cancellationToken)
        {
            var results = await _votingService.GetResultsAsync(code, cancellationToken);

            var csv = new StringBuilder();
            csv.AppendLine("option_index,option_text,votes,percentage");
            foreach (var option in results.Options)
            {
                csv.Append(option.Index.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(EscapeCsv(option.Text)).Append(',')
                   .Append(option.Votes.ToString(CultureInfo.InvariantCulture)).Append(',')
                   .Append(option.Percentage.ToString("0.0", CultureInfo.InvariantCulture))
                   .AppendLine();
            }

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"poll-{results.Code}-results.csv");
        }

        private static string EscapeCsv(string value) =>
            value.Contains(',', StringComparison.Ordinal)
                || value.Contains('"', StringComparison.Ordinal)
                || value.Contains('\n', StringComparison.Ordinal)
                ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
                : value;
    }
}
