using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Infrastructure.Persistence;

public enum StorageProvider
{
    /// <summary>Data lives in memory only — fast, resets on restart. Used by tests.</summary>
    InMemory,

    /// <summary>Data is written to a JSON file and survives restarts. Default for local runs.</summary>
    File
}

/// <summary>Bound from the "Storage" configuration section and validated at startup (fail fast on bad config).</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.File;

    /// <summary>Relative paths resolve against the app's content root.</summary>
    [Required(AllowEmptyStrings = false)]
    public string FilePath { get; set; } = "App_Data/service-requests.json";

    /// <summary>Seed realistic sample data when the store is empty, so the UI is never blank on first run.</summary>
    public bool SeedSampleData { get; set; } = true;
}
