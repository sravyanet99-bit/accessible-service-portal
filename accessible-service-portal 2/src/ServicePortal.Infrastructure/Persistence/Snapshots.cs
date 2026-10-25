using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Infrastructure.Persistence;

/// <summary>
/// Storage shape for a request. Kept separate from the domain model so the domain can evolve
/// without breaking stored data (and vice versa).
/// </summary>
internal sealed record ServiceRequestSnapshot(
    int Id,
    string Subject,
    string Description,
    string Category,
    Priority Priority,
    RequestStatus Status,
    string RequesterName,
    string RequesterEmail,
    string? RequesterPhone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version,
    IReadOnlyList<StatusChangeSnapshot> History)
{
    public static ServiceRequestSnapshot From(ServiceRequest request) => new(
        request.Id,
        request.Subject,
        request.Description,
        request.Category,
        request.Priority,
        request.Status,
        request.Requester.Name,
        request.Requester.Email,
        request.Requester.Phone,
        request.CreatedAt,
        request.UpdatedAt,
        request.Version,
        request.History.Select(h => new StatusChangeSnapshot(h.From, h.To, h.ChangedAt)).ToList());

    public ServiceRequest ToDomain() => ServiceRequest.Restore(
        Id,
        Subject,
        Description,
        new ContactDetails(RequesterName, RequesterEmail, RequesterPhone),
        Category,
        Priority,
        Status,
        CreatedAt,
        UpdatedAt,
        Version,
        History.Select(h => new StatusChange(h.From, h.To, h.ChangedAt)));
}

internal sealed record StatusChangeSnapshot(RequestStatus? From, RequestStatus To, DateTimeOffset ChangedAt);

/// <summary>The whole persisted state: the id sequence plus every request.</summary>
internal sealed record StoreState(int LastId, IReadOnlyList<ServiceRequestSnapshot> Items)
{
    public const int SchemaVersion = 1;

    public int Schema { get; init; } = SchemaVersion;
}
