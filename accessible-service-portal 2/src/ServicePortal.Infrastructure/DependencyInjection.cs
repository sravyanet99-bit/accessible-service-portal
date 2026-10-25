using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServicePortal.Application.Abstractions;
using ServicePortal.Infrastructure.HealthChecks;
using ServicePortal.Infrastructure.Persistence;

namespace ServicePortal.Infrastructure;

public static class DependencyInjection
{
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ISnapshotStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            if (options.Provider == StorageProvider.InMemory)
            {
                return new InMemorySnapshotStore();
            }

            var environment = provider.GetRequiredService<IHostEnvironment>();
            var path = Path.IsPathRooted(options.FilePath)
                ? options.FilePath
                : Path.Combine(environment.ContentRootPath, options.FilePath);
            return new JsonFileSnapshotStore(path, provider.GetRequiredService<ILogger<JsonFileSnapshotStore>>());
        });

        services.AddSingleton<IServiceRequestRepository, ServiceRequestRepository>();

        services.AddHealthChecks()
            .AddCheck<StorageHealthCheck>("storage", tags: [ReadinessTag]);

        return services;
    }
}
