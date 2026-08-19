using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PollBuilder.Contracts.Infrastructure
{
    /// <summary>
    /// Restricts an endpoint to service-to-service calls by requiring the shared
    /// <see cref="ServiceDefaults.InternalApiKeyHeader"/> secret. The gateway never routes these paths,
    /// so this is defence in depth against someone reaching a service host directly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class InternalOnlyAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var expected = context.HttpContext.RequestServices
                .GetRequiredService<IOptions<ServiceEndpointOptions>>().Value.InternalApiKey;

            // An unset key means the deployment has not been configured for internal calls; refuse
            // rather than silently allowing anyone in.
            var presented = context.HttpContext.Request.Headers[ServiceDefaults.InternalApiKeyHeader].ToString();

            if (string.IsNullOrWhiteSpace(expected) || !string.Equals(presented, expected, StringComparison.Ordinal))
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "This endpoint is only callable between services."
                })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            await next();
        }
    }
}
