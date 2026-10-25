using System.Globalization;
using ServicePortal.Domain.Common;

namespace ServicePortal.Domain.ServiceRequests;

/// <summary>
/// Aggregate root for a service request.
/// State only changes through methods that enforce the business rules — there are no public setters.
/// </summary>
public sealed class ServiceRequest
{
    /// <summary>The workflow. Anything not listed here is rejected.</summary>
    private static readonly Dictionary<RequestStatus, RequestStatus[]> AllowedTransitionMap =
        new()
        {
            [RequestStatus.Open] = [RequestStatus.InProgress, RequestStatus.Cancelled],
            [RequestStatus.InProgress] = [RequestStatus.Resolved, RequestStatus.Open, RequestStatus.Cancelled],
            [RequestStatus.Resolved] = [RequestStatus.Open],
            [RequestStatus.Cancelled] = []
        };

    private readonly List<StatusChange> _history;

    private ServiceRequest(
        int id,
        string subject,
        string description,
        ContactDetails requester,
        string category,
        Priority priority,
        RequestStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int version,
        IEnumerable<StatusChange> history)
    {
        Id = id;
        Subject = subject;
        Description = description;
        Requester = requester;
        Category = category;
        Priority = priority;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
        _history = history.ToList();
    }

    public int Id { get; }

    public string Reference => FormatReference(Id);

    public string Subject { get; }

    public string Description { get; }

    public ContactDetails Requester { get; }

    public string Category { get; }

    public Priority Priority { get; }

    public RequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic-concurrency token. Increments on every change; surfaced to clients as an ETag.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<StatusChange> History => _history;

    /// <summary>Statuses this request may move to next — lets the UI offer only valid choices.</summary>
    public IReadOnlyList<RequestStatus> AllowedTransitions => AllowedTransitionMap[Status];

    public static string FormatReference(int id) =>
        string.Create(CultureInfo.InvariantCulture, $"SR-{id:D6}");

    /// <summary>Creates a new, open request. Input is assumed validated by the application layer.</summary>
    public static ServiceRequest Submit(
        int id,
        string subject,
        string description,
        ContactDetails requester,
        string category,
        Priority priority,
        DateTimeOffset submittedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(requester);
        if (!ServiceRequestRules.IsKnownCategory(category))
        {
            throw new ArgumentException($"Unknown category '{category}'.", nameof(category));
        }

        return new ServiceRequest(
            id,
            subject.Trim(),
            description.Trim(),
            requester,
            category,
            priority,
            RequestStatus.Open,
            submittedAt,
            submittedAt,
            version: 1,
            [new StatusChange(null, RequestStatus.Open, submittedAt)]);
    }

    /// <summary>Rebuilds a request from storage. Bypasses workflow rules — for persistence only.</summary>
    public static ServiceRequest Restore(
        int id,
        string subject,
        string description,
        ContactDetails requester,
        string category,
        Priority priority,
        RequestStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int version,
        IEnumerable<StatusChange> history) =>
        new(id, subject, description, requester, category, priority, status, createdAt, updatedAt, version, history);

    /// <summary>Moves the request through the workflow, recording the change in its history.</summary>
    public Result ChangeStatus(RequestStatus newStatus, DateTimeOffset changedAt)
    {
        if (newStatus == Status)
        {
            return ServiceRequestErrors.StatusUnchanged(Status);
        }

        if (!AllowedTransitionMap[Status].Contains(newStatus))
        {
            return ServiceRequestErrors.InvalidTransition(Status, newStatus);
        }

        _history.Add(new StatusChange(Status, newStatus, changedAt));
        Status = newStatus;
        UpdatedAt = changedAt;
        Version++;
        return Result.Success();
    }
}
