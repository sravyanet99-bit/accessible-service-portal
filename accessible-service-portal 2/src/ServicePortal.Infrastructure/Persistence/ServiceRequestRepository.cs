using Microsoft.Extensions.Options;
using ServicePortal.Application.Abstractions;
using ServicePortal.Application.ServiceRequests.Queries;
using ServicePortal.Domain.ServiceRequests;
using ServicePortal.Infrastructure.Seeding;

namespace ServicePortal.Infrastructure.Persistence;

/// <summary>
/// Repository backed by an in-memory index of immutable snapshots, optionally persisted by an
/// <see cref="ISnapshotStore"/>.
///
/// Design notes:
/// • Storing snapshots (not live aggregates) means callers can never mutate stored state by accident;
///   every read returns a fresh aggregate and every write is an explicit replace.
/// • A single async gate serialises writes. Simple and correct for a single instance; a database
///   adapter would use row versions instead (see docs/adr/0003-persistence.md).
/// </summary>
internal sealed class ServiceRequestRepository(
    ISnapshotStore store,
    IOptions<StorageOptions> options,
    TimeProvider clock) : IServiceRequestRepository, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<int, ServiceRequestSnapshot> _items = [];
    private int _lastId;
    private bool _loaded;

    public async Task<int> NextIdAsync(CancellationToken cancellationToken) =>
        await WithGateAsync(() => Task.FromResult(++_lastId), cancellationToken);

    public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WithGateAsync(async () =>
        {
            if (!_items.TryAdd(request.Id, ServiceRequestSnapshot.From(request)))
            {
                throw new InvalidOperationException($"A request with id {request.Id} already exists.");
            }

            _lastId = Math.Max(_lastId, request.Id);
            await PersistAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public Task<ServiceRequest?> FindAsync(int id, CancellationToken cancellationToken) =>
        WithGateAsync(() => Task.FromResult(_items.TryGetValue(id, out var snapshot) ? snapshot.ToDomain() : null),
            cancellationToken);

    public Task<PagedResult<ServiceRequest>> QueryAsync(ServiceRequestQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return WithGateAsync(() =>
        {
            IEnumerable<ServiceRequestSnapshot> results = _items.Values;

            if (query.Status is { } status)
            {
                results = results.Where(item => item.Status == status);
            }

            if (query.Search is { } search)
            {
                results = results.Where(item =>
                    ServiceRequest.FormatReference(item.Id).Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Subject.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.RequesterName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var matches = results.ToList();
            var page = Sort(matches, query.Sort, query.Direction)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(snapshot => snapshot.ToDomain())
                .ToList();

            return Task.FromResult(new PagedResult<ServiceRequest>(page, query.Page, query.PageSize, matches.Count));
        }, cancellationToken);
    }

    public Task<WriteOutcome> UpdateAsync(ServiceRequest request, int expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WithGateAsync(async () =>
        {
            if (!_items.TryGetValue(request.Id, out var current))
            {
                return WriteOutcome.NotFound;
            }

            if (current.Version != expectedVersion)
            {
                return WriteOutcome.VersionMismatch;
            }

            _items[request.Id] = ServiceRequestSnapshot.From(request);
            await PersistAsync(cancellationToken);
            return WriteOutcome.Succeeded;
        }, cancellationToken);
    }

    public Task<WriteOutcome> DeleteAsync(int id, int expectedVersion, CancellationToken cancellationToken) =>
        WithGateAsync(async () =>
        {
            if (!_items.TryGetValue(id, out var current))
            {
                return WriteOutcome.NotFound;
            }

            if (current.Version != expectedVersion)
            {
                return WriteOutcome.VersionMismatch;
            }

            _items.Remove(id);
            await PersistAsync(cancellationToken);
            return WriteOutcome.Succeeded;
        }, cancellationToken);

    public Task<IReadOnlyDictionary<RequestStatus, int>> CountByStatusAsync(CancellationToken cancellationToken) =>
        WithGateAsync(() => Task.FromResult<IReadOnlyDictionary<RequestStatus, int>>(
            _items.Values.GroupBy(item => item.Status).ToDictionary(group => group.Key, group => group.Count())),
            cancellationToken);

    public void Dispose() => _gate.Dispose();

    private static IEnumerable<ServiceRequestSnapshot> Sort(
        IEnumerable<ServiceRequestSnapshot> items, SortField field, SortDirection direction)
    {
        var ordered = (field, direction) switch
        {
            (SortField.Reference, SortDirection.Ascending) => items.OrderBy(i => i.Id),
            (SortField.Reference, _) => items.OrderByDescending(i => i.Id),
            (SortField.Priority, SortDirection.Ascending) => items.OrderBy(i => i.Priority),
            (SortField.Priority, _) => items.OrderByDescending(i => i.Priority),
            (SortField.Status, SortDirection.Ascending) => items.OrderBy(i => i.Status),
            (SortField.Status, _) => items.OrderByDescending(i => i.Status),
            (_, SortDirection.Ascending) => items.OrderBy(i => i.CreatedAt),
            _ => items.OrderByDescending(i => i.CreatedAt)
        };

        // Stable tiebreaker so paging never shows the same row twice or skips one.
        return ordered.ThenByDescending(i => i.Id);
    }

    private async Task<T> WithGateAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Loads persisted state once, on first use. Caller must hold the gate.</summary>
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        var state = await store.LoadAsync(cancellationToken);
        if (state is not null)
        {
            foreach (var item in state.Items)
            {
                _items[item.Id] = item;
            }

            _lastId = Math.Max(state.LastId, _items.Keys.DefaultIfEmpty(0).Max());
        }
        else if (options.Value.SeedSampleData)
        {
            foreach (var request in SampleData.Create(clock.GetUtcNow()))
            {
                _items[request.Id] = ServiceRequestSnapshot.From(request);
            }

            _lastId = _items.Keys.DefaultIfEmpty(0).Max();
            await PersistAsync(cancellationToken);
        }

        _loaded = true;
    }

    private Task PersistAsync(CancellationToken cancellationToken) =>
        store.SaveAsync(new StoreState(_lastId, _items.Values.OrderBy(i => i.Id).ToList()), cancellationToken);
}
