using Microsoft.Extensions.Logging;
using ServicePortal.Application.Abstractions;
using ServicePortal.Application.ServiceRequests.Validation;
using ServicePortal.Domain.Common;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.ServiceRequests;

/// <summary>
/// Application service: orchestrates validation, domain behaviour and persistence for each use case.
/// Returns <see cref="Result{T}"/> for expected outcomes; the API translates those into HTTP responses.
/// </summary>
public sealed partial class ServiceRequestService(
    IServiceRequestRepository repository,
    TimeProvider clock,
    ILogger<ServiceRequestService> logger)
{
    // The .NET 8 logging source generator needs an explicit ILogger field.
    private readonly ILogger<ServiceRequestService> _logger = logger;

    public async Task<Result<ServiceRequestResponse>> SubmitAsync(
        CreateServiceRequestRequest request,
        CancellationToken cancellationToken)
    {
        var validation = CreateServiceRequestValidator.Validate(request);
        if (validation.IsFailure)
        {
            return validation.Error!;
        }

        var input = validation.Value;
        var id = await repository.NextIdAsync(cancellationToken);
        var serviceRequest = ServiceRequest.Submit(
            id, input.Subject, input.Description, input.Requester, input.Category, input.Priority, clock.GetUtcNow());

        await repository.AddAsync(serviceRequest, cancellationToken);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var reference = serviceRequest.Reference;
            var category = serviceRequest.Category;
            var priority = serviceRequest.Priority;
#pragma warning disable CA1873 // LoggerMessage delegates already guard enabled-state; local values are cached here.
            LogSubmitted(reference, category, priority);
#pragma warning restore CA1873
        }

        return serviceRequest.ToResponse();
    }

    public async Task<Result<ServiceRequestResponse>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var serviceRequest = await repository.FindAsync(id, cancellationToken);
        return serviceRequest is null
            ? ServiceRequestErrors.NotFound(id)
            : serviceRequest.ToResponse();
    }

    public async Task<Result<PagedResponse<ServiceRequestListItem>>> ListAsync(
        ListServiceRequestsRequest request,
        CancellationToken cancellationToken)
    {
        var query = QueryParser.Parse(request);
        if (query.IsFailure)
        {
            return query.Error!;
        }

        var page = await repository.QueryAsync(query.Value, cancellationToken);
        return new PagedResponse<ServiceRequestListItem>(
            page.Items.Select(item => item.ToListItem()).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount,
            page.TotalPages);
    }

    public async Task<Result<ServiceRequestResponse>> ChangeStatusAsync(
        int id,
        ChangeStatusRequest request,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is null)
        {
            return ServiceRequestErrors.VersionRequired();
        }

        var status = QueryParser.ParseStatus(request);
        if (status.IsFailure)
        {
            return status.Error!;
        }

        var serviceRequest = await repository.FindAsync(id, cancellationToken);
        if (serviceRequest is null)
        {
            return ServiceRequestErrors.NotFound(id);
        }

        // Fail fast on a stale client before evaluating business rules against stale assumptions.
        if (serviceRequest.Version != expectedVersion)
        {
            return ServiceRequestErrors.ConcurrencyConflict(id);
        }

        var previous = serviceRequest.Status;
        var change = serviceRequest.ChangeStatus(status.Value, clock.GetUtcNow());
        if (change.IsFailure)
        {
            return change.Error!;
        }

        var outcome = await repository.UpdateAsync(serviceRequest, expectedVersion.Value, cancellationToken);
        if (outcome != WriteOutcome.Succeeded)
        {
            return MapWriteFailure(id, outcome);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var reference = serviceRequest.Reference;
            var newStatus = serviceRequest.Status;
#pragma warning disable CA1873 // LoggerMessage delegates already guard enabled-state; local values are cached here.
            LogStatusChanged(reference, previous, newStatus);
#pragma warning restore CA1873
        }

        return serviceRequest.ToResponse();
    }

    public async Task<Result> DeleteAsync(int id, int? expectedVersion, CancellationToken cancellationToken)
    {
        if (expectedVersion is null)
        {
            return ServiceRequestErrors.VersionRequired();
        }

        var outcome = await repository.DeleteAsync(id, expectedVersion.Value, cancellationToken);
        if (outcome != WriteOutcome.Succeeded)
        {
            return MapWriteFailure(id, outcome);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var reference = ServiceRequest.FormatReference(id);
#pragma warning disable CA1873 // LoggerMessage delegates already guard enabled-state; local values are cached here.
            LogDeleted(reference);
#pragma warning restore CA1873
        }

        return Result.Success();
    }

    public async Task<StatusSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var counts = await repository.CountByStatusAsync(cancellationToken);
        var byStatus = Enum.GetValues<RequestStatus>()
            .Select(status => new StatusCountResponse(status, StatusText.Label(status), counts.GetValueOrDefault(status)))
            .ToList();
        return new StatusSummaryResponse(byStatus.Sum(s => s.Count), byStatus);
    }

    private static Error MapWriteFailure(int id, WriteOutcome outcome) => outcome switch
    {
        WriteOutcome.NotFound => ServiceRequestErrors.NotFound(id),
        WriteOutcome.VersionMismatch => ServiceRequestErrors.ConcurrencyConflict(id),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };

    // Source-generated logging: zero allocations when the level is disabled, structured properties always.
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Service request {Reference} submitted (category {Category}, priority {Priority})")]
    private partial void LogSubmitted(string reference, string category, Priority priority);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Service request {Reference} moved from {From} to {To}")]
    private partial void LogStatusChanged(string reference, RequestStatus from, RequestStatus to);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Service request {Reference} deleted")]
    private partial void LogDeleted(string reference);
}
