using ServicePortal.Domain.Common;

namespace ServicePortal.Domain.ServiceRequests;

/// <summary>Catalogue of expected failures for service requests. Codes are part of the public API contract.</summary>
public static class ServiceRequestErrors
{
    public static Error NotFound(int id) => Error.NotFound(
        "service_request.not_found",
        $"Service request {ServiceRequest.FormatReference(id)} was not found. It may have been deleted.");

    public static Error InvalidTransition(RequestStatus from, RequestStatus to) => Error.Conflict(
        "service_request.invalid_status_transition",
        $"A request that is {StatusText.Describe(from)} cannot be changed to {StatusText.Describe(to)}.");

    public static Error StatusUnchanged(RequestStatus status) => Error.Conflict(
        "service_request.status_unchanged",
        $"The request is already {StatusText.Describe(status)}.");

    public static Error ConcurrencyConflict(int id) => Error.PreconditionFailed(
        "service_request.concurrency_conflict",
        $"Service request {ServiceRequest.FormatReference(id)} was changed by someone else. Reload it and try again.");

    public static Error VersionRequired() => Error.PreconditionRequired(
        "service_request.version_required",
        "This change needs the request's current version. Send it in the If-Match header.");
}

/// <summary>Human-readable status names used in messages and published to the UI.</summary>
public static class StatusText
{
    public static string Label(RequestStatus status) => status switch
    {
        RequestStatus.Open => "Open",
        RequestStatus.InProgress => "In progress",
        RequestStatus.Resolved => "Resolved",
        RequestStatus.Cancelled => "Cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string Describe(RequestStatus status) =>
        Label(status).ToLowerInvariant();
}
