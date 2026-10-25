using ServicePortal.Application.ServiceRequests;

namespace ServicePortal.Api.Endpoints;

public static class MetadataEndpoints
{
    public static IEndpointRouteBuilder MapMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/metadata", (HttpContext httpContext) =>
            {
                // Reference data changes only on deploy; let browsers cache it briefly.
                httpContext.Response.Headers.CacheControl = "public, max-age=300";
                return TypedResults.Ok(MetadataResponse.Current);
            })
            .WithTags("Metadata")
            .WithName("GetMetadata")
            .WithSummary("Categories, priorities, statuses and input limits for building the UI.");

        return app;
    }
}
