using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ServicePortal.Infrastructure.Persistence;

/// <summary>Where the repository's state is saved between runs.</summary>
internal interface ISnapshotStore
{
    Task<StoreState?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(StoreState state, CancellationToken cancellationToken);
}

/// <summary>No persistence: state lives only in memory.</summary>
internal sealed class InMemorySnapshotStore : ISnapshotStore
{
    public Task<StoreState?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<StoreState?>(null);

    public Task SaveAsync(StoreState state, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Persists state as JSON. Writes are atomic: data goes to a temporary file which then replaces the
/// original in one step, so a crash mid-write can never leave a half-written, corrupt data file.
/// </summary>
internal sealed partial class JsonFileSnapshotStore(string filePath, ILogger<JsonFileSnapshotStore> logger)
    : ISnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<JsonFileSnapshotStore> _logger = logger;

    public string FilePath { get; } = filePath;

    public async Task<StoreState?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath))
        {
            LogNoDataFile(FilePath);
            return null;
        }

        await using var stream = File.OpenRead(FilePath);
        try
        {
            var state = await JsonSerializer.DeserializeAsync<StoreState>(stream, SerializerOptions, cancellationToken);
            LogLoaded(state?.Items.Count ?? 0, FilePath);
            return state;
        }
        catch (JsonException exception)
        {
            // Never silently overwrite a file we can't read — that would destroy data. Fail loudly instead.
            throw new InvalidDataException($"The data file '{FilePath}' is not valid JSON and was not modified.", exception);
        }
    }

    public async Task SaveAsync(StoreState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "No data file at {FilePath}; starting empty")]
    private partial void LogNoDataFile(string filePath);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Loaded {Count} service requests from {FilePath}")]
    private partial void LogLoaded(int count, string filePath);
}
