using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace PollBuilder.Contracts.Errors
{
    /// <summary>
    /// Shared across all services so every error the SPA sees has the same shape, whichever service
    /// produced it, and so controllers never need try/catch around service calls.
    /// </summary>
    public class ServiceExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly ILogger<ServiceExceptionHandler> _logger;

        public ServiceExceptionHandler(
            IProblemDetailsService problemDetailsService,
            ILogger<ServiceExceptionHandler> logger)
        {
            _problemDetailsService = problemDetailsService;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            if (exception is not ServiceException serviceException)
            {
                return false;
            }

            _logger.LogInformation(
                "Rejected {Method} {Path}: {ErrorCode}",
                httpContext.Request.Method, httpContext.Request.Path, serviceException.ErrorCode);

            httpContext.Response.StatusCode = serviceException.StatusCode;

            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = serviceException.StatusCode,
                    Title = ReasonPhrase(serviceException.StatusCode),
                    Detail = serviceException.Message,
                    Extensions = { ["errorCode"] = serviceException.ErrorCode }
                }
            });
        }

        private static string ReasonPhrase(int statusCode) => statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
            _ => "Error"
        };
    }
}
