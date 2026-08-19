using System.ComponentModel.DataAnnotations;

namespace PollBuilder.Contracts.Polls
{
    /// <summary>Lifecycle state of a poll. Serialised as a string ("Open"/"Closed") over the wire.</summary>
    public enum PollStatus
    {
        Open = 0,
        Closed = 1
    }

    /// <summary>One answer choice.</summary>
    public record PollOptionDto(int Index, string Text);

    /// <summary>
    /// A poll as returned by PollService. Also the shape VotingService deserialises when it validates a
    /// vote, so both services agree on the contract by referencing this one type.
    /// </summary>
    public record PollDto(
        string Code,
        string Question,
        PollStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        DateTimeOffset? ClosedAt,
        bool AcceptsVotes,
        bool HasVotes,
        IReadOnlyList<PollOptionDto> Options);

    /// <summary>
    /// Returned once by <c>POST /api/polls</c>. The only response that ever carries
    /// <see cref="CreatorToken"/> — the service stores only its hash.
    /// </summary>
    public record CreatedPollResponse(
        string Code,
        string Question,
        PollStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        IReadOnlyList<PollOptionDto> Options,
        string CreatorToken,
        string ShareUrl);

    /// <summary>Body of <c>POST /api/polls</c>.</summary>
    public class CreatePollRequest : IValidatableObject
    {
        [Required]
        [StringLength(300, MinimumLength = 3)]
        public string Question { get; set; } = string.Empty;

        /// <summary>Between 2 and 6 answer choices, in display order.</summary>
        [Required]
        public IList<string> Options { get; set; } = [];

        /// <summary>Optional auto-close time; must be in the future.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            PollValidation.ValidateOptions(Options).Concat(PollValidation.ValidateExpiry(ExpiresAt));
    }

    /// <summary>
    /// Body of <c>PUT /api/polls/{code}</c>. A full replacement: the stored poll ends up exactly as
    /// described here.
    /// </summary>
    public class UpdatePollRequest : IValidatableObject
    {
        [Required]
        [StringLength(300, MinimumLength = 3)]
        public string Question { get; set; } = string.Empty;

        [Required]
        public IList<string> Options { get; set; } = [];

        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Target lifecycle state, <c>Open</c> or <c>Closed</c>.</summary>
        [Required]
        public PollStatus Status { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            PollValidation.ValidateOptions(Options);
    }

    /// <summary>
    /// Body of <c>PATCH /api/polls/{code}</c>. Every property is optional and omitted properties are
    /// left unchanged. This is a JSON merge patch rather than RFC 6902, which keeps requests readable
    /// for the SPA and avoids adding a Newtonsoft dependency purely for patching.
    /// </summary>
    public class PatchPollRequest : IValidatableObject
    {
        [StringLength(300, MinimumLength = 3)]
        public string? Question { get; set; }

        public IList<string>? Options { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Set true to remove an existing expiry (omitting <c>expiresAt</c> means "unchanged").</summary>
        public bool ClearExpiresAt { get; set; }

        public PollStatus? Status { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Question is null && Options is null && ExpiresAt is null && !ClearExpiresAt && Status is null)
            {
                yield return new ValidationResult(
                    "Provide at least one of question, options, expiresAt, clearExpiresAt or status.");
            }

            if (ExpiresAt is not null && ClearExpiresAt)
            {
                yield return new ValidationResult(
                    "expiresAt and clearExpiresAt cannot be used together.", [nameof(ClearExpiresAt)]);
            }

            if (Options is not null)
            {
                foreach (var result in PollValidation.ValidateOptions(Options))
                {
                    yield return result;
                }
            }
        }
    }

    /// <summary>Option and expiry rules, shared so create, put and patch cannot drift apart.</summary>
    public static class PollValidation
    {
        public const int MinOptions = 2;
        public const int MaxOptions = 6;
        public const int MaxOptionLength = 200;

        public static IEnumerable<ValidationResult> ValidateOptions(IList<string>? options)
        {
            if (options is null || options.Count < MinOptions || options.Count > MaxOptions)
            {
                yield return new ValidationResult(
                    $"A poll needs between {MinOptions} and {MaxOptions} options.", [nameof(CreatePollRequest.Options)]);
                yield break;
            }

            if (options.Any(string.IsNullOrWhiteSpace))
            {
                yield return new ValidationResult("Options cannot be blank.", [nameof(CreatePollRequest.Options)]);
            }

            if (options.Any(o => o?.Length > MaxOptionLength))
            {
                yield return new ValidationResult(
                    $"Options cannot be longer than {MaxOptionLength} characters.", [nameof(CreatePollRequest.Options)]);
            }

            if (options.Where(o => !string.IsNullOrWhiteSpace(o))
                       .Select(o => o.Trim())
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .Count() != options.Count)
            {
                yield return new ValidationResult("Options must be unique.", [nameof(CreatePollRequest.Options)]);
            }
        }

        public static IEnumerable<ValidationResult> ValidateExpiry(DateTimeOffset? expiresAt)
        {
            if (expiresAt is not null && expiresAt <= DateTimeOffset.UtcNow)
            {
                yield return new ValidationResult(
                    "expiresAt must be in the future.", [nameof(CreatePollRequest.ExpiresAt)]);
            }
        }
    }
}
