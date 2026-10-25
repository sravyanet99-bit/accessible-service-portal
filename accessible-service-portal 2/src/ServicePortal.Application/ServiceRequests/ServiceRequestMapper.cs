using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.ServiceRequests;

/// <summary>Domain → contract mapping. Hand-written: explicit, fast, and trivially debuggable.</summary>
internal static class ServiceRequestMapper
{
    public static ServiceRequestResponse ToResponse(this ServiceRequest request) => new(
        request.Id,
        request.Reference,
        request.Subject,
        request.Description,
        request.Category,
        request.Priority,
        request.Status,
        new RequesterResponse(request.Requester.Name, request.Requester.Email, request.Requester.Phone),
        request.AllowedTransitions,
        request.History.Select(h => new StatusChangeResponse(h.From, h.To, h.ChangedAt)).ToList(),
        request.CreatedAt,
        request.UpdatedAt,
        request.Version);

    public static ServiceRequestListItem ToListItem(this ServiceRequest request) => new(
        request.Id,
        request.Reference,
        request.Subject,
        request.Category,
        request.Priority,
        request.Status,
        request.Requester.Name,
        request.CreatedAt,
        request.Version);
}
