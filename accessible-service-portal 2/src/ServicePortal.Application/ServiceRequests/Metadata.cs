using ServicePortal.Application.ServiceRequests.Queries;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.ServiceRequests;

/// <summary>
/// Reference data and limits for clients. The UI builds its selects, radio groups and character
/// counters from this, so business rules live in exactly one place: the server.
/// </summary>
public sealed record MetadataResponse(
    IReadOnlyList<string> Categories,
    IReadOnlyList<OptionResponse> Priorities,
    IReadOnlyList<OptionResponse> Statuses,
    LimitsResponse Limits)
{
    public static MetadataResponse Current { get; } = new(
        ServiceRequestRules.Categories,
        [
            new(nameof(Priority.Low), "Low", "General question or minor issue."),
            new(nameof(Priority.Medium), "Medium", "Something isn't working, but there is a workaround."),
            new(nameof(Priority.High), "High", "I can't complete an essential task.")
        ],
        Enum.GetValues<RequestStatus>()
            .Select(status => new OptionResponse(status.ToString(), StatusText.Label(status), null))
            .ToList(),
        new LimitsResponse(
            ServiceRequestRules.SubjectMaxLength,
            ServiceRequestRules.DescriptionMinLength,
            ServiceRequestRules.DescriptionMaxLength,
            ServiceRequestRules.NameMaxLength,
            ServiceRequestRules.EmailMaxLength,
            ServiceRequestRules.PhoneMaxLength,
            ServiceRequestQuery.DefaultPageSize,
            ServiceRequestQuery.MaxPageSize));
}

public sealed record OptionResponse(string Value, string Label, string? Description);

public sealed record LimitsResponse(
    int SubjectMaxLength,
    int DescriptionMinLength,
    int DescriptionMaxLength,
    int NameMaxLength,
    int EmailMaxLength,
    int PhoneMaxLength,
    int DefaultPageSize,
    int MaxPageSize);
