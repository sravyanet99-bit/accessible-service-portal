namespace ServicePortal.Domain.ServiceRequests;

/// <summary>One entry in a request's audit trail. <see cref="From"/> is null for the initial submission.</summary>
public sealed record StatusChange(RequestStatus? From, RequestStatus To, DateTimeOffset ChangedAt);
