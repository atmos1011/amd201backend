using Microsoft.AspNetCore.Http;

namespace PollBuilder.Contracts.Errors
{
    /// <summary>
    /// Base type for expected business-rule failures. <see cref="ServiceExceptionHandler"/> turns each
    /// one into an RFC 7807 ProblemDetails response using the status code carried here, which keeps HTTP
    /// concerns out of the domain services and makes the rules unit-testable.
    /// </summary>
    public abstract class ServiceException(string message, int statusCode, string errorCode)
        : Exception(message)
    {
        public int StatusCode { get; } = statusCode;

        /// <summary>Stable machine-readable code the SPA can branch on, e.g. "already_voted".</summary>
        public string ErrorCode { get; } = errorCode;
    }

    public sealed class PollNotFoundException(string code)
        : ServiceException($"No poll exists with code '{code}'.", StatusCodes.Status404NotFound, "poll_not_found");

    public sealed class PollClosedException(string code)
        : ServiceException($"Poll '{code}' is no longer accepting votes.", StatusCodes.Status409Conflict, "poll_closed");

    public sealed class DuplicateVoteException(string code)
        : ServiceException($"This respondent has already voted in poll '{code}'.", StatusCodes.Status409Conflict, "already_voted");

    public sealed class InvalidOptionException(int optionIndex)
        : ServiceException($"Option index {optionIndex} does not exist on this poll.", StatusCodes.Status400BadRequest, "invalid_option");

    public sealed class NotPollCreatorException()
        : ServiceException("A valid X-Creator-Token header is required to modify this poll.", StatusCodes.Status403Forbidden, "not_creator");

    public sealed class PollHasVotesException()
        : ServiceException("The question and options cannot be changed once voting has started.", StatusCodes.Status409Conflict, "poll_has_votes");

    public sealed class PollCodeGenerationException()
        : ServiceException("Could not allocate a unique poll code. Please retry.", StatusCodes.Status503ServiceUnavailable, "code_generation_failed");

    /// <summary>Raised when a downstream service is unreachable or answers with an unexpected status.</summary>
    public sealed class UpstreamServiceException(string service)
        : ServiceException($"The {service} service is unavailable. Please try again.", StatusCodes.Status503ServiceUnavailable, "upstream_unavailable");
}
