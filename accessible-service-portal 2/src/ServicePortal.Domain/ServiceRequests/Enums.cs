namespace ServicePortal.Domain.ServiceRequests;

public enum Priority
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum RequestStatus
{
    Open = 1,
    InProgress = 2,
    Resolved = 3,
    Cancelled = 4
}
