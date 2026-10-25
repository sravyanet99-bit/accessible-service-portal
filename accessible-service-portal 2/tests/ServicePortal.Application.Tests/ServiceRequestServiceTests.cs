using Microsoft.Extensions.Logging.Abstractions;
using ServicePortal.Application.ServiceRequests;
using ServicePortal.Domain.Common;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.Tests;

public sealed class ServiceRequestServiceTests
{
    private readonly FakeRepository _repository = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));
    private readonly ServiceRequestService _service;

    public ServiceRequestServiceTests() =>
        _service = new ServiceRequestService(_repository, _clock, NullLogger<ServiceRequestService>.Instance);

    [Fact]
    public async Task Submit_Valid_PersistsAndReturnsOpenRequest()
    {
        var result = await _service.SubmitAsync(CreateServiceRequestValidatorTests.Valid(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(RequestStatus.Open, result.Value.Status);
        Assert.Equal(_clock.Now, result.Value.CreatedAt);
        Assert.Equal(new[] { RequestStatus.InProgress, RequestStatus.Cancelled }, result.Value.AllowedTransitions);
        Assert.Single(_repository.Items);
    }

    [Fact]
    public async Task Submit_Invalid_PersistsNothing()
    {
        var result = await _service.SubmitAsync(CreateServiceRequestValidatorTests.Valid() with { Subject = "" }, default);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task ChangeStatus_WithoutVersion_RequiresPrecondition()
    {
        var created = await SubmitAsync();

        var result = await _service.ChangeStatusAsync(created.Id, new ChangeStatusRequest("InProgress"), null, default);

        Assert.Equal(ErrorType.PreconditionRequired, result.Error!.Type);
    }

    [Fact]
    public async Task ChangeStatus_WithStaleVersion_IsAConcurrencyConflict()
    {
        var created = await SubmitAsync();

        var result = await _service.ChangeStatusAsync(created.Id, new ChangeStatusRequest("InProgress"), created.Version + 5, default);

        Assert.Equal("service_request.concurrency_conflict", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeStatus_WhenAnotherWriterWinsTheRace_IsAConcurrencyConflict()
    {
        var created = await SubmitAsync();
        _repository.SimulateConcurrentEdit = true;

        var result = await _service.ChangeStatusAsync(created.Id, new ChangeStatusRequest("InProgress"), created.Version, default);

        Assert.Equal(ErrorType.PreconditionFailed, result.Error!.Type);
    }

    [Fact]
    public async Task ChangeStatus_Valid_AdvancesVersionAndRecordsHistory()
    {
        var created = await SubmitAsync();
        _clock.Now = _clock.Now.AddHours(2);

        var result = await _service.ChangeStatusAsync(created.Id, new ChangeStatusRequest("InProgress"), created.Version, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Version + 1, result.Value.Version);
        Assert.Equal(2, result.Value.History.Count);
        Assert.Equal(_clock.Now, result.Value.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatus_UnknownRequest_IsNotFound()
    {
        var result = await _service.ChangeStatusAsync(999, new ChangeStatusRequest("InProgress"), 1, default);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Delete_WithCurrentVersion_RemovesRequest()
    {
        var created = await SubmitAsync();

        var result = await _service.DeleteAsync(created.Id, created.Version, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task Summary_CountsEveryStatus_IncludingZeroes()
    {
        await SubmitAsync();

        var summary = await _service.GetSummaryAsync(default);

        Assert.Equal(1, summary.Total);
        Assert.Equal(4, summary.ByStatus.Count);
        Assert.Equal("In progress", summary.ByStatus.Single(s => s.Status == RequestStatus.InProgress).Label);
    }

    private async Task<ServiceRequestResponse> SubmitAsync() =>
        (await _service.SubmitAsync(CreateServiceRequestValidatorTests.Valid(), default)).Value;
}
