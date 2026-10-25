using ServicePortal.Application.Abstractions;
using ServicePortal.Application.ServiceRequests.Queries;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.Tests;

/// <summary>Deterministic clock for tests.</summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// Minimal in-memory implementation of the persistence port. Because the application only depends
/// on <see cref="IServiceRequestRepository"/>, use cases are tested with no infrastructure at all.
/// </summary>
internal sealed class FakeRepository : IServiceRequestRepository
{
    private readonly Dictionary<int, ServiceRequest> _items = [];
    private int _lastId = 100;

    /// <summary>Simulates another user changing the record between our read and our write.</summary>
    public bool SimulateConcurrentEdit { get; set; }

    public IReadOnlyCollection<ServiceRequest> Items => _items.Values;

    public Task<int> NextIdAsync(CancellationToken cancellationToken) => Task.FromResult(++_lastId);

    public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        _items.Add(request.Id, request);
        return Task.CompletedTask;
    }

    public Task<ServiceRequest?> FindAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(id, out var item) ? Clone(item) : null);

    public Task<PagedResult<ServiceRequest>> QueryAsync(ServiceRequestQuery query, CancellationToken cancellationToken)
    {
        var matches = _items.Values.Where(i => query.Status is null || i.Status == query.Status).ToList();
        var page = matches.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult(new PagedResult<ServiceRequest>(page, query.Page, query.PageSize, matches.Count));
    }

    public Task<WriteOutcome> UpdateAsync(ServiceRequest request, int expectedVersion, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(request.Id, out var current))
        {
            return Task.FromResult(WriteOutcome.NotFound);
        }

        if (SimulateConcurrentEdit || current.Version != expectedVersion)
        {
            return Task.FromResult(WriteOutcome.VersionMismatch);
        }

        _items[request.Id] = request;
        return Task.FromResult(WriteOutcome.Succeeded);
    }

    public Task<WriteOutcome> DeleteAsync(int id, int expectedVersion, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(id, out var current))
        {
            return Task.FromResult(WriteOutcome.NotFound);
        }

        if (current.Version != expectedVersion)
        {
            return Task.FromResult(WriteOutcome.VersionMismatch);
        }

        _items.Remove(id);
        return Task.FromResult(WriteOutcome.Succeeded);
    }

    public Task<IReadOnlyDictionary<RequestStatus, int>> CountByStatusAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<RequestStatus, int>>(
            _items.Values.GroupBy(i => i.Status).ToDictionary(g => g.Key, g => g.Count()));

    private static ServiceRequest Clone(ServiceRequest r) => ServiceRequest.Restore(
        r.Id, r.Subject, r.Description, r.Requester, r.Category, r.Priority, r.Status,
        r.CreatedAt, r.UpdatedAt, r.Version, r.History);
}
