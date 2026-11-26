using FiveAndOne.API.Errors;
using FiveAndOne.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FiveAndOne.API.Middleware;

/// <summary>
/// Catches every unhandled exception and turns it into the standard <c>ErrorResponse</c> body.
/// Known exceptions keep a meaningful status (400/401/404/409...); anything else becomes a 500
/// without leaking internals (details are only added in Development).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            _logger.LogWarning(exception, "Response already started; cannot write error body.");
            return false;
        }

        // Client disconnected: nothing useful to send back.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (status, code, message) = Map(exception);
        var errors = (exception as AppException)?.Errors;

        if (status >= 500)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogInformation("Request failed with {Status} ({Code}): {Message}", status, code, exception.Message);
        }

        var detail = _environment.IsDevelopment() && status >= 500 ? exception.ToString() : null;

        await ErrorResponseFactory.WriteAsync(httpContext, status, message, code, errors, detail, (exception as AppException)?.Details);
        return true;
    }

    private static (int Status, string? Code, string? Message) Map(Exception exception) => exception switch
    {
        // Our own expected errors (BadRequest/Unauthorized/NotFound/Conflict) keep their message.
        AppException app => (app.StatusCode, app.Code, app.Message),

        // Malformed request the framework rejected (bad JSON, body too large, ...).
        BadHttpRequestException bad => (bad.StatusCode, null, null),

        // Someone else changed the row between our read and write (xmin row version).
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrency_conflict",
            "This record was changed by someone else. Please reload and try again."),

        // Database constraint violations.
        DbUpdateException { InnerException: PostgresException pg } => pg.SqlState switch
        {
            PostgresErrorCodes.ExclusionViolation => (StatusCodes.Status409Conflict, "slot_unavailable",
                "This time slot is no longer available. Please choose another time."),
            PostgresErrorCodes.UniqueViolation => (StatusCodes.Status409Conflict, "duplicate",
                "A record with the same value already exists."),
            PostgresErrorCodes.ForeignKeyViolation => (StatusCodes.Status400BadRequest, "invalid_reference",
                "A referenced record does not exist or is still in use."),
            PostgresErrorCodes.CheckViolation => (StatusCodes.Status400BadRequest, "invalid_data",
                "The data violates a validation rule."),
            _ => (StatusCodes.Status500InternalServerError, null, null)
        },

        _ => (StatusCodes.Status500InternalServerError, null, null)
    };
}
