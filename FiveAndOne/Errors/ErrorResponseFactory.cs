using FiveAndOne.Application.Dtos.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Diagnostics;

namespace FiveAndOne.API.Errors;

/// <summary>Builds and writes <see cref="ErrorResponse"/> bodies so every error path looks the same.</summary>
public static class ErrorResponseFactory
{
    public static ErrorResponse Create(
        HttpContext httpContext,
        int status,
        string? message = null,
        string? code = null,
        IDictionary<string, string[]>? errors = null,
        string? detail = null,
        object? details = null)
    {
        return new ErrorResponse
        {
            Status = status,
            Code = code ?? ErrorResponse.CodeFor(status),
            Message = message ?? ErrorResponse.DefaultMessageFor(status),
            Errors = errors,
            Details = details,
            Detail = detail,
            TraceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };
    }

    public static Task WriteAsync(
        HttpContext httpContext,
        int status,
        string? message = null,
        string? code = null,
        IDictionary<string, string[]>? errors = null,
        string? detail = null,
        object? details = null)
    {
        httpContext.Response.StatusCode = status;
        return httpContext.Response.WriteAsJsonAsync(
            Create(httpContext, status, message, code, errors, detail, details),
            options: null,
            contentType: "application/json");
    }

    /// <summary>Used by [ApiController] when model binding / DataAnnotations validation fails.</summary>
    public static IActionResult FromInvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => ToFieldName(entry.Key),
                entry => entry.Value!.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "The value is invalid." : e.ErrorMessage)
                    .ToArray());

        // When the JSON itself is broken, MVC also adds a generic "<parameter> is required" entry. Drop it
        // so the client only sees the real problem.
        if (errors.Count > 1)
        {
            foreach (var parameter in context.ActionDescriptor.Parameters)
            {
                errors.Remove(ToFieldName(parameter.Name));
            }
        }

        var body = Create(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            "One or more validation errors occurred.",
            "validation_failed",
            errors);

        return new BadRequestObjectResult(body) { ContentTypes = { "application/json" } };
    }

    // "Email" -> "email", "$.role" -> "role", "Items[0].Name" -> "items[0].name"
    private static string ToFieldName(string key)
    {
        if (key.StartsWith("$.", StringComparison.Ordinal))
        {
            key = key[2..];
        }
        else if (key == "$")
        {
            return "body";
        }

        return string.Join('.', key.Split('.').Select(part =>
            part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
    }
}
