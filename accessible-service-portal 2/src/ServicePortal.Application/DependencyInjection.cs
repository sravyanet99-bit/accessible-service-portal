using Microsoft.Extensions.DependencyInjection;
using ServicePortal.Application.ServiceRequests;

namespace ServicePortal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ServiceRequestService>();
        return services;
    }
}
