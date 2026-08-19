using PollBuilder.Contracts.Errors;
using PollBuilder.Contracts.Infrastructure;
using PollBuilder.Contracts.Polls;
using PollBuilder.Polls.Models;
using PollBuilder.Polls.Repo;
using PollBuilder.Polls.Services;

namespace PollBuilder.Polls.Services
{
    /// <summary>
    /// Poll business rules: code allocation, creator-only edits, expiry and the lifecycle. It depends
    /// only on interfaces, so the whole class is unit-testable without a database.
    /// </summary>
    public class PollService : IPollService
    {
        /// <summary>How many codes to try before giving up, in the (vanishingly unlikely) collision case.</summary>
        private const int MaxCodeAttempts = 5;

        private readonly IPollRepo _repository;
        private readonly IPollCodeGenerator _codeGenerator;
        private readonly IRealtimeNotifier _notifier;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PollService> _logger;

        public PollService(
            IPollRepo repository,
            IPollCodeGenerator codeGenerator,
            IRealtimeNotifier notifier,
            TimeProvider timeProvider,
            ILogger<PollService> logger)
        {
            _repository = repository;
            _codeGenerator = codeGenerator;
            _notifier = notifier;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<CreatedPollResponse> CreateAsync(
            CreatePollRequest request, string shareBaseUrl, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var creatorToken = TokenGenerator.Create();

            var poll = new Poll
            {
                Code = await AllocateCodeAsync(cancellationToken),
                Question = request.Question.Trim(),
                Status = PollStatus.Open,
                CreatedAt = _timeProvider.GetUtcNow(),
                ExpiresAt = request.ExpiresAt?.ToUniversalTime(),
                CreatorTokenHash = TokenGenerator.Hash(creatorToken),
                Options = [.. request.Options.Select((text, index) => new PollOption
                {
                    OptionIndex = index,
                    Text = text.Trim()
                })]
            };

            await _repository.AddPollAsync(poll, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created poll {Code} with {OptionCount} options", poll.Code, poll.Options.Count);

            return new CreatedPollResponse(
                poll.Code,
                poll.Question,
                poll.Status,
                poll.CreatedAt,
                poll.ExpiresAt,
                MapOptions(poll),
                creatorToken,
                BuildShareUrl(shareBaseUrl, poll.Code));
        }

        public async Task<PollDto> GetAsync(string code, CancellationToken cancellationToken = default) =>
            Map(await LoadAsync(code, cancellationToken));

        public async Task<PollDto> ReplaceAsync(
            string code, UpdatePollRequest request, string? creatorToken, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var poll = await LoadForCreatorAsync(code, creatorToken, cancellationToken);

            await ApplyContentAsync(poll, request.Question, request.Options, cancellationToken);

            poll.ExpiresAt = request.ExpiresAt?.ToUniversalTime();
            var justClosed = ApplyStatus(poll, request.Status);

            await _repository.SaveChangesAsync(cancellationToken);
            await NotifyIfClosedAsync(poll, justClosed, cancellationToken);
            return Map(poll);
        }

        public async Task<PollDto> PatchAsync(
            string code, PatchPollRequest request, string? creatorToken, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var poll = await LoadForCreatorAsync(code, creatorToken, cancellationToken);

            await ApplyContentAsync(poll, request.Question, request.Options, cancellationToken);

            if (request.ClearExpiresAt)
            {
                poll.ExpiresAt = null;
            }
            else if (request.ExpiresAt is not null)
            {
                poll.ExpiresAt = request.ExpiresAt.Value.ToUniversalTime();
            }

            var justClosed = request.Status is not null && ApplyStatus(poll, request.Status.Value);

            await _repository.SaveChangesAsync(cancellationToken);
            await NotifyIfClosedAsync(poll, justClosed, cancellationToken);
            return Map(poll);
        }

        public Task<PollDto> CloseAsync(string code, string? creatorToken, CancellationToken cancellationToken = default) =>
            PatchAsync(code, new PatchPollRequest { Status = PollStatus.Closed }, creatorToken, cancellationToken);

        public async Task MarkHasVotesAsync(string code, CancellationToken cancellationToken = default)
        {
            var poll = await _repository.GetByCodeAsync(code, cancellationToken)
                ?? throw new PollNotFoundException(code);

            if (poll.HasVotes)
            {
                return;
            }

            poll.HasVotes = true;
            await _repository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Poll {Code} locked for editing: first vote recorded", code);
        }

        /// <summary>
        /// Loads a poll by code, lazily flipping it to Closed if its expiry has passed. Checking on read
        /// avoids needing a background job just to retire expired polls.
        /// </summary>
        private async Task<Poll> LoadAsync(string code, CancellationToken cancellationToken)
        {
            var poll = await _repository.GetByCodeAsync(code, cancellationToken)
                ?? throw new PollNotFoundException(code);

            var now = _timeProvider.GetUtcNow();
            if (poll.Status == PollStatus.Open && poll.IsExpired(now))
            {
                poll.Status = PollStatus.Closed;
                poll.ClosedAt = poll.ExpiresAt ?? now;
                await _repository.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Poll {Code} auto-closed at its expiry time", poll.Code);
            }

            return poll;
        }

        private async Task<Poll> LoadForCreatorAsync(string code, string? creatorToken, CancellationToken cancellationToken)
        {
            var poll = await LoadAsync(code, cancellationToken);

            if (!TokenGenerator.Matches(creatorToken, poll.CreatorTokenHash))
            {
                throw new NotPollCreatorException();
            }

            return poll;
        }

        /// <summary>Applies question/option edits, which are only legal before the first vote lands.</summary>
        private async Task ApplyContentAsync(
            Poll poll, string? question, IList<string>? options, CancellationToken cancellationToken)
        {
            var changesQuestion = question is not null
                && !string.Equals(question.Trim(), poll.Question, StringComparison.Ordinal);
            var changesOptions = options is not null && !OptionsMatch(poll, options);

            if ((changesQuestion || changesOptions) && poll.HasVotes)
            {
                // Rewriting an option after people have voted would silently reassign their votes to
                // different text, so the edit is refused instead.
                throw new PollHasVotesException();
            }

            if (question is not null)
            {
                poll.Question = question.Trim();
            }

            if (changesOptions)
            {
                await _repository.ReplaceOptionsAsync(poll, [.. options!], cancellationToken);
            }
        }

        private static bool OptionsMatch(Poll poll, IList<string> options)
        {
            var current = poll.Options.OrderBy(o => o.OptionIndex).Select(o => o.Text).ToList();
            return current.Count == options.Count
                && current.Zip(options, (a, b) => string.Equals(a, b.Trim(), StringComparison.Ordinal)).All(same => same);
        }

        /// <summary>Moves the poll to <paramref name="status"/>. Returns true only if it just became Closed.</summary>
        private bool ApplyStatus(Poll poll, PollStatus status)
        {
            if (status == poll.Status)
            {
                return false;
            }

            var now = _timeProvider.GetUtcNow();
            poll.Status = status;
            poll.ClosedAt = status == PollStatus.Closed ? now : null;

            // Reopening a poll whose expiry has already passed would immediately auto-close it again.
            if (status == PollStatus.Open && poll.IsExpired(now))
            {
                poll.ExpiresAt = null;
            }

            return status == PollStatus.Closed;
        }

        /// <summary>
        /// Pushes the close out over SignalR so results pages disable voting without a refresh.
        /// Auto-close on expiry deliberately does not broadcast: nobody triggered it, and every client
        /// discovers it on its next request anyway.
        /// </summary>
        private async Task NotifyIfClosedAsync(Poll poll, bool justClosed, CancellationToken cancellationToken)
        {
            if (justClosed)
            {
                await _notifier.PollClosedAsync(poll.Code, poll.ClosedAt, cancellationToken);
            }
        }

        private async Task<string> AllocateCodeAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
            {
                var candidate = _codeGenerator.Next();
                if (!await _repository.CodeExistsAsync(candidate, cancellationToken))
                {
                    return candidate;
                }

                _logger.LogWarning("Poll code {Code} already taken, retrying (attempt {Attempt})", candidate, attempt + 1);
            }

            throw new PollCodeGenerationException();
        }

        private PollDto Map(Poll poll) =>
            new(poll.Code,
                poll.Question,
                poll.Status,
                poll.CreatedAt,
                poll.ExpiresAt,
                poll.ClosedAt,
                poll.AcceptsVotes(_timeProvider.GetUtcNow()),
                poll.HasVotes,
                MapOptions(poll));

        private static IReadOnlyList<PollOptionDto> MapOptions(Poll poll) =>
            [.. poll.Options.OrderBy(o => o.OptionIndex).Select(o => new PollOptionDto(o.OptionIndex, o.Text))];

        private static string BuildShareUrl(string shareBaseUrl, string code) =>
            $"{shareBaseUrl.TrimEnd('/')}/poll/{code}";
    }
}
