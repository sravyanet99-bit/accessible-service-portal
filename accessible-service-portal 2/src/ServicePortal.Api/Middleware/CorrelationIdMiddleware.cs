using System.Diagnostics;

namespace ServicePortal.Api.Middleware;

/// <summary>
/// Gives every request a correlation id (accepted from the caller or generated), echoes it in the
/// response, scopes all log entries to it, and logs one structured line per API request.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaxLength = 64;

    private readonly ILogger<CorrelationIdMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = ResolveCorrelationId(context);
        context.Items[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(new CorrelationScope(correlationId));
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    var method = context.Request.Method;
                    var path = context.Request.Path;
                    var statusCode = context.Response.StatusCode;
                    var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
#pragma warning disable CA1873 // LoggerMessage delegates already guard enabled-state; local values are cached here.
                    LogRequestCompleted(method, path, statusCode, elapsedMs);
#pragma warning restore CA1873
                }
            }
        }
    }

    /// <summary>Accept a caller's id only if it is short and safe to log; otherwise generate one.</summary>
    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var isSafe = incoming.Length is > 0 and <= MaxLength
            && incoming.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
        return isSafe ? incoming : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Log scope that JSON formatters emit as a structured "CorrelationId" property and plain-text
    /// formatters print readably (via ToString).
    /// </summary>
    private sealed class CorrelationScope(string correlationId) : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly KeyValuePair<string, object> _item = new("CorrelationId", correlationId);

        public int Count => 1;

        public KeyValuePair<string, object> this[int index] =>
            index == 0 ? _item : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return _item;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"CorrelationId:{_item.Value}";
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms")]
    private partial void LogRequestCompleted(string method, string path, int statusCode, double elapsedMs);
}
