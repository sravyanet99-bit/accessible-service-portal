using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.ServiceRequests.Queries;

public enum SortField
{
    CreatedAt,
    Reference,
    Priority,
    Status
}

public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>A validated, normalised list query (filters, sort, page).</summary>
public sealed record ServiceRequestQuery(
    RequestStatus? Status,
    string? Search,
    SortField Sort,
    SortDirection Direction,
    int Page,
    int PageSize)
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    public static ServiceRequestQuery Default { get; } =
        new(null, null, SortField.CreatedAt, SortDirection.Descending, 1, DefaultPageSize);
}

/// <summary>One page of results plus the numbers a pagination control needs.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
        new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}
