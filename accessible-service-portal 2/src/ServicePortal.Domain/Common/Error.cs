namespace ServicePortal.Domain.Common;

/// <summary>Categories of expected failure. The API layer maps each one to an HTTP status code.</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    PreconditionFailed,
    PreconditionRequired
}

/// <summary>
/// An expected, recoverable failure (bad input, missing record, rule violation).
/// Unexpected failures are exceptions; expected ones are values, so callers must handle them.
/// </summary>
/// <param name="Code">Stable, machine-readable identifier (e.g. <c>service_request.not_found</c>).</param>
/// <param name="Message">Human-readable message, safe to show to end users.</param>
/// <param name="Type">Failure category.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>Per-field messages for validation failures, keyed by camelCase field name.</summary>
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; init; } = NoFieldErrors;

    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("validation_failed", "Some fields need your attention.", ErrorType.Validation)
        {
            FieldErrors = fieldErrors
        };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error PreconditionFailed(string code, string message) =>
        new(code, message, ErrorType.PreconditionFailed);

    public static Error PreconditionRequired(string code, string message) =>
        new(code, message, ErrorType.PreconditionRequired);
}
