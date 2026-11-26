namespace FiveAndOne.Application.Common.Exceptions;

/// <summary>
/// Base class for expected errors. The API's exception handler turns these into the standard
/// ErrorResponse body using <see cref="StatusCode"/>, <see cref="Code"/>, the message, <see cref="Errors"/>
/// and <see cref="Details"/>.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode, string? code = null,
        IDictionary<string, string[]>? errors = null, object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Errors = errors;
        Details = details;
    }

    public int StatusCode { get; }

    /// <summary>Optional specific error code; defaults to one derived from the status (e.g. "conflict").</summary>
    public string? Code { get; }

    /// <summary>Optional field-level errors (field name → messages).</summary>
    public IDictionary<string, string[]>? Errors { get; }

    /// <summary>Optional structured payload, e.g. the reservations that conflict with a closure.</summary>
    public object? Details { get; }
}

public sealed class BadRequestException : AppException
{
    public BadRequestException(string message, string? code = null) : base(message, 400, code) { }
}

/// <summary>400 with field-level errors, for rules checked in services (not just DataAnnotations).</summary>
public sealed class RequestValidationException : AppException
{
    public RequestValidationException(string message, IDictionary<string, string[]> errors, string code = "validation_failed")
        : base(message, 400, code, errors) { }

    public static RequestValidationException For(string field, string message, string code = "validation_failed") =>
        new(message, new Dictionary<string, string[]> { [field] = new[] { message } }, code);
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message, string? code = null) : base(message, 401, code) { }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message, string? code = null) : base(message, 403, code) { }
}

/// <summary>The business's plan doesn't allow this (e.g. too many facilities).</summary>
public sealed class PlanLimitExceededException : AppException
{
    public PlanLimitExceededException(string message, string code = "plan_limit_reached") : base(message, 403, code) { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message, string? code = null) : base(message, 404, code) { }
}

/// <summary>A feature flag is off: behaves like the endpoint doesn't exist.</summary>
public sealed class FeatureDisabledException : AppException
{
    public FeatureDisabledException(string key)
        : base("This feature is not available.", 404, "feature_disabled", details: new { feature = key }) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message, string? code = null, object? details = null) : base(message, 409, code, details: details) { }
}

/// <summary>429: a booking limit or abuse control stopped the request.</summary>
public sealed class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string message, string code = "too_many_requests") : base(message, 429, code) { }
}
