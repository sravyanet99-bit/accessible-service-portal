using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ServicePortal.Api.Configuration;
using ServicePortal.Api.Http;
using ServicePortal.Application.ServiceRequests;

namespace ServicePortal.Api.Endpoints;

/// <summary>
/// <c>/api/v1/service-requests</c> — versioned in the URL so v2 can ship without breaking v1 clients.
/// Endpoints stay thin: bind HTTP → call the application service → translate the result to HTTP.
/// </summary>
public static class ServiceRequestEndpoints
{
    public const string GetByIdRouteName = "GetServiceRequest";

    public static IEndpointRouteBuilder MapServiceRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/service-requests").WithTags("Service requests");

        group.MapGet("/", ListAsync)
            .WithName("ListServiceRequests")
            .WithSummary("List requests with filtering, search, sorting and paging.");

        group.MapGet("/summary", SummaryAsync)
            .WithName("GetServiceRequestSummary")
            .WithSummary("Count requests by status.");

        group.MapGet("/{id:int}", GetAsync)
            .WithName(GetByIdRouteName)
            .WithSummary("Get one request, including its history. Supports If-None-Match.");

        group.MapPost("/", SubmitAsync)
            .WithName("SubmitServiceRequest")
            .WithSummary("Submit a new request.")
            .RequireRateLimiting(RateLimitingOptions.SubmissionsPolicy);

        group.MapPatch("/{id:int}/status", ChangeStatusAsync)
            .WithName("ChangeServiceRequestStatus")
            .WithSummary("Move a request through the workflow. Requires If-Match.");

        group.MapDelete("/{id:int}", DeleteAsync)
            .WithName("DeleteServiceRequest")
            .WithSummary("Delete a request. Requires If-Match.");

        return app;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ServiceRequestService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            new ListServiceRequestsRequest(status, search, sort, page, pageSize), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem(httpContext);
    }

    private static async Task<Ok<StatusSummaryResponse>> SummaryAsync(
        ServiceRequestService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetSummaryAsync(cancellationToken));

    private static async Task<IResult> GetAsync(
        int id,
        ServiceRequestService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem(httpContext);
        }

        var etag = EntityTag.For(result.Value.Version);
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.CacheControl = "no-cache";

        return EntityTag.Matches(httpContext.Request.Headers.IfNoneMatch, etag)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(result.Value);
    }

    private static async Task<IResult> SubmitAsync(
        CreateServiceRequestRequest request,
        ServiceRequestService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await service.SubmitAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem(httpContext);
        }

        httpContext.Response.Headers.ETag = EntityTag.For(result.Value.Version);
        return TypedResults.CreatedAtRoute(result.Value, GetByIdRouteName, new { id = result.Value.Id });
    }

    private static async Task<IResult> ChangeStatusAsync(
        int id,
        ChangeStatusRequest request,
        ServiceRequestService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var expectedVersion = EntityTag.ParseIfMatch(httpContext.Request.Headers.IfMatch);
        var result = await service.ChangeStatusAsync(id, request, expectedVersion, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem(httpContext);
        }

        httpContext.Response.Headers.ETag = EntityTag.For(result.Value.Version);
        return TypedResults.Ok(result.Value);
    }

    private static async Task<IResult> DeleteAsync(
        int id,
        ServiceRequestService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var expectedVersion = EntityTag.ParseIfMatch(httpContext.Request.Headers.IfMatch);
        var result = await service.DeleteAsync(id, expectedVersion, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem(httpContext);
    }
}
