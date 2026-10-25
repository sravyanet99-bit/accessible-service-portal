using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServicePortal.Application.Abstractions;

namespace ServicePortal.Infrastructure.HealthChecks;

/// <summary>Readiness probe: the app is only "ready" if its data store can be read.</summary>
internal sealed class StorageHealthCheck(IServiceRequestRepository repository) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var counts = await repository.CountByStatusAsync(cancellationToken);
            return HealthCheckResult.Healthy($"Storage reachable ({counts.Values.Sum()} requests).");
        }
#pragma warning disable CA1031 // A health check must report any failure rather than throw.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("Storage is not reachable.", exception);
        }
    }
}
