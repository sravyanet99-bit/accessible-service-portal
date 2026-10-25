using ServicePortal.Application.ServiceRequests.Queries;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.Abstractions;

/// <summary>
/// Persistence port. The application depends on this contract; Infrastructure provides the adapter
/// (in-memory, JSON file, or a database) — swapping storage never touches business code.
/// </summary>
public interface IServiceRequestRepository
{
    Task<int> NextIdAsync(CancellationToken cancellationToken);

    Task AddAsync(ServiceRequest request, CancellationToken cancellationToken);

    Task<ServiceRequest?> FindAsync(int id, CancellationToken cancellationToken);

    Task<PagedResult<ServiceRequest>> QueryAsync(ServiceRequestQuery query, CancellationToken cancellationToken);

    /// <summary>Saves changes only if the stored version still equals <paramref name="expectedVersion"/>.</summary>
    Task<WriteOutcome> UpdateAsync(ServiceRequest request, int expectedVersion, CancellationToken cancellationToken);

    /// <summary>Deletes only if the stored version still equals <paramref name="expectedVersion"/>.</summary>
    Task<WriteOutcome> DeleteAsync(int id, int expectedVersion, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<RequestStatus, int>> CountByStatusAsync(CancellationToken cancellationToken);
}

public enum WriteOutcome
{
    Succeeded,
    NotFound,
    VersionMismatch
}
