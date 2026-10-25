using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.ServiceRequests;

// ---------------------------------------------------------------------------------------------
// Request contracts use strings rather than enums on purpose: a bad value like "Urgent!!" then
// reaches our validator and produces a friendly field message, instead of a serializer exception.
// ---------------------------------------------------------------------------------------------

public sealed record CreateServiceRequestRequest(
    string? Subject,
    string? Category,
    string? Priority,
    string? Description,
    string? RequesterName,
    string? RequesterEmail,
    string? RequesterPhone);

public sealed record ChangeStatusRequest(string? Status);

public sealed record ListServiceRequestsRequest(
    string? Status,
    string? Search,
    string? Sort,
    int? Page,
    int? PageSize);

// ---------------------------------------------------------------------------------------------
// Response contracts
// ---------------------------------------------------------------------------------------------

public sealed record ServiceRequestResponse(
    int Id,
    string Reference,
    string Subject,
    string Description,
    string Category,
    Priority Priority,
    RequestStatus Status,
    RequesterResponse Requester,
    IReadOnlyList<RequestStatus> AllowedTransitions,
    IReadOnlyList<StatusChangeResponse> History,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record RequesterResponse(string Name, string Email, string? Phone);

public sealed record StatusChangeResponse(RequestStatus? From, RequestStatus To, DateTimeOffset ChangedAt);

/// <summary>Lightweight row for list views — no description or history, keeps payloads small.</summary>
public sealed record ServiceRequestListItem(
    int Id,
    string Reference,
    string Subject,
    string Category,
    Priority Priority,
    RequestStatus Status,
    string RequesterName,
    DateTimeOffset CreatedAt,
    int Version);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record StatusSummaryResponse(int Total, IReadOnlyList<StatusCountResponse> ByStatus);

public sealed record StatusCountResponse(RequestStatus Status, string Label, int Count);
