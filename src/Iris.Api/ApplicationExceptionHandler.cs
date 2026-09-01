using Iris.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Iris.Api;

/// <summary>Maps application-layer exceptions to RFC 7807 problem responses, logging every one.</summary>
public sealed class ApplicationExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApplicationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ValidationException or InvalidScopeRequestException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            // Nothing recognised it — this is a bug, not an expected client error. Log it with
            // full context before letting the default problem-details middleware take over, so
            // it leaves a trace instead of silently vanishing (see the "customer" lock-type gap).
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path} (trace {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier);
            return false;
        }

        // Expected client errors (bad input, missing/conflicting resource) — worth a trail, not an alarm.
        logger.LogWarning(
            exception,
            "{Title} on {Method} {Path} (trace {TraceId}): {Message}",
            title, httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier, exception.Message);

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
            },
        }).ConfigureAwait(false);
    }
}
