using ServicePortal.Domain.Common;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Domain.Tests;

public sealed class ServiceRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static ServiceRequest NewRequest(int id = 7) => ServiceRequest.Submit(
        id,
        "  Signing link has expired  ",
        "The link in my envelope says the session expired.",
        new ContactDetails("Alex Morgan", "alex@example.com", "  "),
        "Document signing",
        Priority.High,
        Now);

    [Fact]
    public void Submit_CreatesOpenRequestAtVersionOne_WithInitialHistoryEntry()
    {
        var request = NewRequest();

        Assert.Equal(RequestStatus.Open, request.Status);
        Assert.Equal(1, request.Version);
        Assert.Equal(Now, request.CreatedAt);
        Assert.Equal(Now, request.UpdatedAt);
        var entry = Assert.Single(request.History);
        Assert.Null(entry.From);
        Assert.Equal(RequestStatus.Open, entry.To);
    }

    [Fact]
    public void Submit_TrimsText_AndTreatsBlankPhoneAsNone()
    {
        var request = NewRequest();

        Assert.Equal("Signing link has expired", request.Subject);
        Assert.Null(request.Requester.Phone);
    }

    [Theory]
    [InlineData(7, "SR-000007")]
    [InlineData(1013, "SR-001013")]
    [InlineData(1234567, "SR-1234567")]
    public void Reference_IsZeroPaddedToSixDigits(int id, string expected)
    {
        Assert.Equal(expected, ServiceRequest.FormatReference(id));
    }

    [Fact]
    public void Submit_RejectsUnknownCategory()
    {
        Assert.Throws<ArgumentException>(() => ServiceRequest.Submit(
            1, "Subject", "Description text", new ContactDetails("A", "a@b.co", null), "Made up", Priority.Low, Now));
    }

    [Theory]
    [InlineData(RequestStatus.Open, RequestStatus.InProgress)]
    [InlineData(RequestStatus.Open, RequestStatus.Cancelled)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Resolved)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Open)]
    [InlineData(RequestStatus.Resolved, RequestStatus.Open)]
    public void ChangeStatus_AllowsWorkflowTransitions(RequestStatus from, RequestStatus to)
    {
        var request = InStatus(from);
        var versionBefore = request.Version;

        var result = request.ChangeStatus(to, Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(to, request.Status);
        Assert.Equal(versionBefore + 1, request.Version);
        Assert.Equal(Now.AddHours(1), request.UpdatedAt);
        Assert.Equal(new StatusChange(from, to, Now.AddHours(1)), request.History[^1]);
    }

    [Theory]
    [InlineData(RequestStatus.Open, RequestStatus.Resolved)]
    [InlineData(RequestStatus.Resolved, RequestStatus.Cancelled)]
    [InlineData(RequestStatus.Cancelled, RequestStatus.Open)]
    public void ChangeStatus_RejectsTransitionsOutsideTheWorkflow(RequestStatus from, RequestStatus to)
    {
        var request = InStatus(from);
        var versionBefore = request.Version;

        var result = request.ChangeStatus(to, Now.AddHours(1));

        Assert.True(result.IsFailure);
        Assert.Equal("service_request.invalid_status_transition", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(from, request.Status);
        Assert.Equal(versionBefore, request.Version);
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_IsAConflictNotANoOp()
    {
        var request = NewRequest();

        var result = request.ChangeStatus(RequestStatus.Open, Now);

        Assert.Equal("service_request.status_unchanged", result.Error!.Code);
    }

    [Fact]
    public void AllowedTransitions_ForCancelled_IsEmpty()
    {
        var request = InStatus(RequestStatus.Cancelled);

        Assert.Empty(request.AllowedTransitions);
    }

    private static ServiceRequest InStatus(RequestStatus status)
    {
        var request = NewRequest();
        var path = status switch
        {
            RequestStatus.Open => Array.Empty<RequestStatus>(),
            RequestStatus.InProgress => [RequestStatus.InProgress],
            RequestStatus.Resolved => [RequestStatus.InProgress, RequestStatus.Resolved],
            RequestStatus.Cancelled => [RequestStatus.Cancelled],
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        foreach (var step in path)
        {
            Assert.True(request.ChangeStatus(step, Now).IsSuccess);
        }

        return request;
    }
}
