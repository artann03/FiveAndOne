using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FiveAndOne.Application.Dtos.Common;

/// <summary>
/// The single error body returned by the API for every non-success response (400, 401, 403, 404, 409, 500, ...).
/// </summary>
public class ErrorResponse
{
    /// <summary>HTTP status code, repeated in the body for convenience.</summary>
    public int Status { get; init; }

    /// <summary>Stable machine-readable code, e.g. "validation_failed", "not_found", "conflict".</summary>
    public string Code { get; init; } = default!;

    /// <summary>Human-readable message that is safe to show to end users.</summary>
    public string Message { get; init; } = default!;

    /// <summary>Field-level validation errors (field name → messages). Only present for validation failures.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Structured extra information for some errors, e.g. the reservations that conflict with a closure.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }

    /// <summary>Exception details. Only included in Development, never in production.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }

    /// <summary>Correlates the response with server logs.</summary>
    public string? TraceId { get; init; }

    public static string CodeFor(int status) => status switch
    {
        400 => "bad_request",
        401 => "unauthorized",
        403 => "forbidden",
        404 => "not_found",
        405 => "method_not_allowed",
        409 => "conflict",
        415 => "unsupported_media_type",
        422 => "unprocessable_entity",
        429 => "too_many_requests",
        >= 500 => "internal_error",
        _ => "error"
    };

    public static string DefaultMessageFor(int status) => status switch
    {
        400 => "The request is invalid.",
        401 => "Authentication is required.",
        403 => "You do not have permission to perform this action.",
        404 => "The requested resource was not found.",
        405 => "This HTTP method is not allowed for this endpoint.",
        409 => "The request conflicts with the current state of the resource.",
        415 => "Unsupported content type. Use application/json.",
        429 => "Too many requests. Please try again later.",
        >= 500 => "An unexpected error occurred. Please try again later.",
        _ => "The request could not be processed."
    };
}
