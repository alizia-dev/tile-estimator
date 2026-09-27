using Microsoft.AspNetCore.Mvc;
using TileEstimator.Application.Common;
using TileEstimator.Domain.Common;

namespace TileEstimator.Api.Middleware;

/// <summary>
/// SPEC 19: turns every exception into an RFC 7807 ProblemDetails response carrying the
/// correlation id.
/// <para>
/// Known exception types map to a specific status and their own message, because those messages
/// are written for the user. Anything else becomes a flat 500 with a generic message: stack
/// traces, SQL text and connection strings never reach the client, only the log.
/// </para>
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string
                            ?? context.TraceIdentifier;

        var problem = BuildProblem(exception, correlationId);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

            // A developer running locally gets the detail; a deployed environment never does.
            if (environment.IsDevelopment())
            {
                problem.Extensions["exception"] = exception.GetType().Name;
                problem.Extensions["detail"] = exception.Message;
            }
        }
        else
        {
            logger.LogWarning("Request rejected: {Message}. CorrelationId: {CorrelationId}",
                exception.Message, correlationId);
        }

        if (context.Response.HasStarted)
        {
            logger.LogWarning("The response had already started; the error could not be written as ProblemDetails.");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }

    private static ProblemDetails BuildProblem(Exception exception, string correlationId)
    {
        ProblemDetails problem = exception switch
        {
            ValidationFailedException validation => new ValidationProblemDetails(validation.Errors)
            {
                Title = "Validation error",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            },

            DomainException domain => new ProblemDetails
            {
                Title = "The request is not valid",
                Detail = domain.Message,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            },

            NotFoundException notFound => new ProblemDetails
            {
                Title = "Not found",
                Detail = notFound.Message,
                Status = StatusCodes.Status404NotFound,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5"
            },

            ForbiddenException forbidden => new ProblemDetails
            {
                Title = "Not permitted",
                Detail = forbidden.Message,
                Status = StatusCodes.Status403Forbidden,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            },

            // A cross-tenant attempt is reported as a plain 404-style "not found" to the caller
            // via Forbidden, and the real reason is only written to the log.
            TenantViolationException => new ProblemDetails
            {
                Title = "Not permitted",
                Detail = "You do not have access to that item.",
                Status = StatusCodes.Status403Forbidden,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            },

            ConflictException conflict => new ProblemDetails
            {
                Title = "Conflict",
                Detail = conflict.Message,
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
            },

            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => new ProblemDetails
            {
                Title = "This item was changed by someone else",
                Detail = "Someone else saved changes while you were working. Reload and try again.",
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
            },

            OperationCanceledException => new ProblemDetails
            {
                Title = "Request cancelled",
                Status = StatusCodesExtensions.Status499ClientClosedRequest
            },

            _ => new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Detail = "Something went wrong on our side. Quote the trace id if you contact support.",
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            }
        };

        problem.Extensions["traceId"] = correlationId;
        return problem;
    }
}

/// <summary>Status code for a client that hung up mid-request; not in the framework's constants.</summary>
internal static class StatusCodesExtensions
{
    public const int Status499ClientClosedRequest = 499;
}
