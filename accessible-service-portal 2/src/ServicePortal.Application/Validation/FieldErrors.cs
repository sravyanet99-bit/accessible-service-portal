using ServicePortal.Domain.Common;

namespace ServicePortal.Application.Validation;

/// <summary>
/// Collects validation messages, keeping only the FIRST problem per field.
/// One clear message per field reads far better than a stack of them — especially through a screen reader.
/// </summary>
internal sealed class FieldErrors
{
    private readonly Dictionary<string, string[]> _errors = new(StringComparer.Ordinal);

    public bool Any => _errors.Count > 0;

    public bool Has(string field) => _errors.ContainsKey(field);

    public void Add(string field, string message) => _errors.TryAdd(field, [message]);

    public Error ToError() => Error.Validation(_errors);
}
