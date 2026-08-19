using PollBuilder.Contracts.Errors;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Polls;
using PollBuilder.Contracts.Voting;
using PollBuilder.Voting.Models;
using PollBuilder.Voting.Repo;
using PollBuilder.Voting.Services;

namespace PollBuilder.Voting.Services
{
    /// <summary>
    /// Vote business rules: one vote per respondent, only into an option that exists, only while the
    /// poll is open. The poll itself is fetched from PollService, so this service never assumes anything
    /// about a poll it does not own.
    /// </summary>
    public class VotingService : IVotingService
    {
        private readonly IVoteRepo _repository;
        private readonly IPollCatalog _pollCatalog;
        private readonly IPollNotifier _notifier;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<VotingService> _logger;

        public VotingService(
            IVoteRepo repository,
            IPollCatalog pollCatalog,
            IPollNotifier notifier,
            TimeProvider timeProvider,
            ILogger<VotingService> logger)
        {
            _repository = repository;
            _pollCatalog = pollCatalog;
            _notifier = notifier;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<VoteResponse> VoteAsync(
            string code, int optionIndex, string? voterToken, CancellationToken cancellationToken = default)
        {
            var poll = await LoadPollAsync(code, cancellationToken);

            if (!poll.AcceptsVotes)
            {
                throw new PollClosedException(code);
            }

            if (!poll.Options.Any(o => o.Index == optionIndex))
            {
                throw new InvalidOptionException(optionIndex);
            }

            // A first-time respondent arrives without a token; issue one and hand it back so the SPA can
            // store it. No login is required, which is what the brief asks for.
            var token = string.IsNullOrWhiteSpace(voterToken) ? TokenGenerator.Create() : voterToken.Trim();

            if (await _repository.HasVotedAsync(code, token, cancellationToken))
            {
                throw new DuplicateVoteException(code);
            }

            var inserted = await _repository.TryAddVoteAsync(
                new Vote
                {
                    PollCode = code,
                    OptionIndex = optionIndex,
                    VoterToken = token,
                    VotedAt = _timeProvider.GetUtcNow()
                },
                cancellationToken);

            if (!inserted)
            {
                // Lost a race against a concurrent submit from the same browser.
                throw new DuplicateVoteException(code);
            }

            _logger.LogInformation("Vote recorded for poll {Code}, option {OptionIndex}", code, optionIndex);

            // Let PollService lock the poll's content. A failure here must not undo a recorded vote, so
            // the call swallows its own errors.
            await _pollCatalog.NotifyVotesRecordedAsync(code, cancellationToken);

            var results = await BuildResultsAsync(poll, cancellationToken);
            await _notifier.ResultsUpdatedAsync(results, cancellationToken);

            return new VoteResponse(token, results);
        }

        public async Task<PollResultsResponse> GetResultsAsync(string code, CancellationToken cancellationToken = default)
        {
            var poll = await LoadPollAsync(code, cancellationToken);
            return await BuildResultsAsync(poll, cancellationToken);
        }

        public async Task<bool> HasVotedAsync(string code, string? voterToken, CancellationToken cancellationToken = default) =>
            !string.IsNullOrWhiteSpace(voterToken)
            && await _repository.HasVotedAsync(code, voterToken.Trim(), cancellationToken);

        private async Task<PollDto> LoadPollAsync(string code, CancellationToken cancellationToken) =>
            await _pollCatalog.GetPollAsync(code, cancellationToken)
            ?? throw new PollNotFoundException(code);

        /// <summary>
        /// Joins the tallies this service owns to the option text PollService owns. Options with no
        /// votes still appear, with a count of zero, so the chart does not change shape as votes arrive.
        /// </summary>
        private async Task<PollResultsResponse> BuildResultsAsync(PollDto poll, CancellationToken cancellationToken)
        {
            var tallies = await _repository.GetTallyAsync(poll.Code, cancellationToken);
            var counts = tallies.ToDictionary(t => t.OptionIndex, t => t.Count);
            var total = counts.Values.Sum();

            var options = poll.Options
                .OrderBy(o => o.Index)
                .Select(o =>
                {
                    var votes = counts.GetValueOrDefault(o.Index);
                    var percentage = total == 0 ? 0 : Math.Round(votes * 100d / total, 1);
                    return new OptionResultResponse(o.Index, o.Text, votes, percentage);
                })
                .ToList();

            return new PollResultsResponse(
                poll.Code,
                poll.Question,
                poll.Status,
                poll.AcceptsVotes,
                total,
                _timeProvider.GetUtcNow(),
                options);
        }
    }
}
