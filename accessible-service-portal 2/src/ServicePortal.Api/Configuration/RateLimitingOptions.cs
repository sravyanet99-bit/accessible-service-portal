using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Api.Configuration;

/// <summary>Limits how often one client can submit new requests (protects against spam and accidental floods).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public const string SubmissionsPolicy = "submissions";

    [Range(1, 10_000)]
    public int SubmissionPermitLimit { get; set; } = 10;

    [Range(1, 3600)]
    public int SubmissionWindowSeconds { get; set; } = 60;
}
