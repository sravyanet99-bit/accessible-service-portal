using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServicePortal.Api.Errors;
using ServicePortal.Api.Http;

namespace ServicePortal.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        // RFC 9457 Problem Details for every error, enriched with trace + correlation ids for support.
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                ProblemDetailsEnricher.Enrich(context.HttpContext, context.ProblemDetails));
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Malformed JSON / wrong types become exceptions our handler turns into a clean 400.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddHealthChecks();
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        AddRateLimiting(services, configuration);
        return services;
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiter, settings) =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                limiter.AddPolicy(RateLimitingOptions.SubmissionsPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.Value.SubmissionPermitLimit,
                            Window = TimeSpan.FromSeconds(settings.Value.SubmissionWindowSeconds),
                            QueueLimit = 0
                        }));

                limiter.OnRejected = async (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                    }

                    var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                    await problemDetails.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many requests.",
                            Detail = "You've sent several requests in a short time. Wait a minute, then try again.",
                            Extensions = { ["code"] = "rate_limited" }
                        }
                    });
                };
            });
    }
}
