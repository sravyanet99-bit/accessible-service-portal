using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ServicePortal.Api.Middleware;
using ServicePortal.Domain.Common;

namespace ServicePortal.Api.Http;

/// <summary>Translates domain <see cref="Error"/>s into HTTP Problem Details responses.</summary>
public static class ProblemResults
{
    public static IResult ToProblem(this Error error, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(httpContext);

        var extensions = ProblemDetailsEnricher.Extensions(httpContext);
        extensions["code"] = error.Code;

        if (error.Type == ErrorType.Validation)
        {
            return TypedResults.ValidationProblem(
                error.FieldErrors.ToDictionary(StringComparer.Ordinal),
                title: error.Message,
                instance: httpContext.Request.Path,
                extensions: extensions);
        }

        var (status, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not found."),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "The change isn't allowed."),
            ErrorType.PreconditionFailed => (StatusCodes.Status412PreconditionFailed, "The request was changed by someone else."),
            ErrorType.PreconditionRequired => (StatusCodes.Status428PreconditionRequired, "A version is required."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error.")
        };

        return TypedResults.Problem(
            detail: error.Message,
            instance: httpContext.Request.Path,
            statusCode: status,
            title: title,
            extensions: extensions);
    }
}

/// <summary>Adds the identifiers support staff need to find a failing request in the logs.</summary>
public static class ProblemDetailsEnricher
{
    public static void Enrich(HttpContext httpContext, ProblemDetails problemDetails)
    {
        ArgumentNullException.ThrowIfNull(problemDetails);
        problemDetails.Instance ??= httpContext?.Request.Path;
        foreach (var (key, value) in Extensions(httpContext))
        {
            problemDetails.Extensions.TryAdd(key, value);
        }
    }

    public static Dictionary<string, object?> Extensions(HttpContext? httpContext) => new(StringComparer.Ordinal)
    {
        ["traceId"] = Activity.Current?.Id ?? httpContext?.TraceIdentifier,
        ["correlationId"] = httpContext?.Items[CorrelationIdMiddleware.HeaderName] as string
    };
}
