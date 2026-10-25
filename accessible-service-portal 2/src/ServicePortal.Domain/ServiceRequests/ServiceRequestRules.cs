namespace ServicePortal.Domain.ServiceRequests;

/// <summary>
/// Single source of truth for input limits and reference data.
/// The API publishes these through <c>GET /api/v1/metadata</c> so the UI never hard-codes them.
/// </summary>
public static class ServiceRequestRules
{
    public const int SubjectMaxLength = 120;
    public const int DescriptionMinLength = 20;
    public const int DescriptionMaxLength = 2000;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 20;

    public static readonly IReadOnlyList<string> Categories =
    [
        "Account access",
        "Document signing",
        "Billing",
        "Accessibility barrier",
        "Other"
    ];

    public static bool IsKnownCategory(string? value) =>
        value is not null && Categories.Contains(value, StringComparer.Ordinal);
}
