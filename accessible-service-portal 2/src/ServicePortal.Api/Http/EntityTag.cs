using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace ServicePortal.Api.Http;

/// <summary>
/// ETag helpers for optimistic concurrency (RFC 9110 §8.8.3, §13.1).
/// The ETag is the resource version: <c>"v3"</c>. Clients send it back in <c>If-Match</c> when changing
/// a resource; if someone else changed it first, the versions differ and the server answers 412.
/// </summary>
public static class EntityTag
{
    public static string For(int version) => string.Create(CultureInfo.InvariantCulture, $"\"v{version}\"");

    /// <summary>
    /// Reads the version from an <c>If-Match</c> header.
    /// Returns null when the header is absent, and 0 (never a real version) when it is present but malformed,
    /// so a malformed header fails the concurrency check instead of being ignored.
    /// </summary>
    public static int? ParseIfMatch(StringValues header)
    {
        if (StringValues.IsNullOrEmpty(header))
        {
            return null;
        }

        var value = header.ToString().Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        value = value.Trim('"');
        return value.StartsWith('v')
               && int.TryParse(value.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : 0;
    }

    /// <summary>True when an <c>If-None-Match</c> header already names the current ETag (→ 304 Not Modified).</summary>
    public static bool Matches(StringValues ifNoneMatch, string etag) =>
        ifNoneMatch.Any(value => value is not null
            && value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(candidate => candidate == "*" || candidate == etag || candidate == $"W/{etag}"));
}
