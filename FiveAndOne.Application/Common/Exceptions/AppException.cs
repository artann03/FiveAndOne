using System;
using System.Collections.Generic;

namespace FiveAndOne.Application.Common.Exceptions;

/// <summary>
/// Base class for expected errors. The API's exception handler turns these into the standard
/// ErrorResponse body using <see cref="StatusCode"/>, <see cref="Code"/>, the message and <see cref="Errors"/>.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode, string? code = null, IDictionary<string, string[]>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Errors = errors;
    }

    public int StatusCode { get; }

    /// <summary>Optional specific error code; defaults to one derived from the status (e.g. "conflict").</summary>
    public string? Code { get; }

    /// <summary>Optional field-level errors (field name → messages).</summary>
    public IDictionary<string, string[]>? Errors { get; }
}

public sealed class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, 400) { }
}

/// <summary>400 with field-level errors, for rules checked in services (not just DataAnnotations).</summary>
public sealed class RequestValidationException : AppException
{
    public RequestValidationException(string message, IDictionary<string, string[]> errors, string code = "validation_failed")
        : base(message, 400, code, errors) { }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message, 401) { }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message, string? code = null) : base(message, 403, code) { }
}

/// <summary>The business's plan doesn't allow this (e.g. too many facilities).</summary>
public sealed class PlanLimitExceededException : AppException
{
    public PlanLimitExceededException(string message) : base(message, 403, "plan_limit_reached") { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message, string? code = null) : base(message, 409, code) { }
}
