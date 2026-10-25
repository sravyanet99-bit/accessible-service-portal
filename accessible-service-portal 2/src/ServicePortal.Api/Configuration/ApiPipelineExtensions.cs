using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ServicePortal.Api.Endpoints;
using ServicePortal.Api.Middleware;
using ServicePortal.Infrastructure;

namespace ServicePortal.Api.Configuration;

public static class ApiPipelineExtensions
{
    /// <summary>Middleware order matters: each component wraps everything registered after it.</summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();      // Outermost: catches anything thrown below.
        app.UseStatusCodePages();       // Bare 404/405s also get a Problem Details body.

        if (app.Environment.IsProduction())
        {
            // TLS terminates at the ingress/load balancer; HSTS tells browsers to stay on HTTPS.
            app.UseHsts();
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRateLimiter();
        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Liveness: is the process up? Readiness: can it serve traffic (storage reachable)?
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DependencyInjection.ReadinessTag)
        });

        app.MapMetadataEndpoints();
        app.MapServiceRequestEndpoints();
        return app;
    }
}
