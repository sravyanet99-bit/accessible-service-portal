using ServicePortal.Application.ServiceRequests;
using ServicePortal.Application.ServiceRequests.Queries;
using ServicePortal.Application.ServiceRequests.Validation;
using ServicePortal.Domain.ServiceRequests;

namespace ServicePortal.Application.Tests;

public sealed class QueryParserTests
{
    [Fact]
    public void NoParameters_UsesNewestFirstDefaults()
    {
        var result = QueryParser.Parse(new ListServiceRequestsRequest(null, null, null, null, null));

        Assert.Equal(ServiceRequestQuery.Default, result.Value);
    }

    [Theory]
    [InlineData("-priority", SortField.Priority, SortDirection.Descending)]
    [InlineData("priority", SortField.Priority, SortDirection.Ascending)]
    [InlineData("STATUS", SortField.Status, SortDirection.Ascending)]
    [InlineData("-createdAt", SortField.CreatedAt, SortDirection.Descending)]
    public void Sort_SupportsMinusPrefixForDescending(string sort, SortField field, SortDirection direction)
    {
        var result = QueryParser.Parse(new ListServiceRequestsRequest(null, null, sort, null, null));

        Assert.Equal(field, result.Value.Sort);
        Assert.Equal(direction, result.Value.Direction);
    }

    [Fact]
    public void InvalidValues_ReportEveryProblemAtOnce()
    {
        var result = QueryParser.Parse(new ListServiceRequestsRequest("Nope", null, "bogus", 0, 500));

        Assert.Equal(new[] { "status", "sort", "page", "pageSize" }, result.Error!.FieldErrors.Keys.ToArray());
    }

    [Fact]
    public void Search_IsTrimmed_AndBlankBecomesNull()
    {
        Assert.Equal("screen", QueryParser.Parse(new ListServiceRequestsRequest(null, "  screen ", null, null, null)).Value.Search);
        Assert.Null(QueryParser.Parse(new ListServiceRequestsRequest(null, "   ", null, null, null)).Value.Search);
    }

    [Fact]
    public void Status_IsCaseInsensitive_ButNumericValuesAreRejected()
    {
        Assert.Equal(RequestStatus.InProgress,
            QueryParser.Parse(new ListServiceRequestsRequest("inprogress", null, null, null, null)).Value.Status);
        Assert.True(QueryParser.Parse(new ListServiceRequestsRequest("2", null, null, null, null)).IsFailure);
    }
}
