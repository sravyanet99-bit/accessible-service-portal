using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace ServicePortal.Api.IntegrationTests;

public sealed class ServiceRequestsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Base = "/api/v1/service-requests";
    private readonly HttpClient _client = factory.CreateClient();

    // ---- Reading -------------------------------------------------------------------------------

    [Fact]
    public async Task List_ReturnsFirstPage_WithPagingMetadata()
    {
        var response = await _client.GetAsync($"{Base}?pageSize=5");
        var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.True(body.GetProperty("totalPages").GetInt32() >= 2);
    }

    [Fact]
    public async Task List_SortsByPriorityDescending()
    {
        var body = await (await _client.GetAsync($"{Base}?sort=-priority&pageSize=50")).ReadJsonAsync();
        var ranks = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("priority").GetString() switch { "High" => 3, "Medium" => 2, _ => 1 })
            .ToList();

        Assert.Equal(ranks.OrderByDescending(rank => rank), ranks);
    }

    [Fact]
    public async Task List_FiltersByStatus_AndSearches()
    {
        var body = await (await _client.GetAsync($"{Base}?status=Open&search=signing")).ReadJsonAsync();

        Assert.All(body.GetProperty("items").EnumerateArray(), item =>
            Assert.Equal("Open", item.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task List_InvalidQuery_Returns400WithEveryProblem()
    {
        var response = await _client.GetAsync($"{Base}?status=Nope&sort=bogus&pageSize=500");
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("status", out _));
        Assert.True(errors.TryGetProperty("sort", out _));
        Assert.True(errors.TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task Get_ReturnsETag_AndHonoursIfNoneMatch()
    {
        var created = await CreateAsync();
        var first = await _client.GetAsync($"{Base}/{created.Id}");
        var etag = first.Headers.ETag!.Tag;

        using var conditional = new HttpRequestMessage(HttpMethod.Get, $"{Base}/{created.Id}");
        conditional.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await _client.SendAsync(conditional);

        Assert.Equal("\"v1\"", etag);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Get_Unknown_Returns404ProblemWithCode()
    {
        var response = await _client.GetAsync($"{Base}/999999");
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("service_request.not_found", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    // ---- Creating ------------------------------------------------------------------------------

    [Fact]
    public async Task Post_Valid_Returns201_WithLocationAndETag()
    {
        var response = await _client.PostAsJsonAsync(Base, Requests.Valid());
        var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"{Base}/{body.GetProperty("id").GetInt32()}", response.Headers.Location!.ToString());
        Assert.Equal("\"v1\"", response.Headers.ETag!.Tag);
        Assert.StartsWith("SR-", body.GetProperty("reference").GetString());
        Assert.Equal("Open", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Post_Invalid_Returns400_WithOneHumanMessagePerField()
    {
        var response = await _client.PostAsJsonAsync(Base, new { subject = "", priority = "Urgent!!", requesterEmail = "bad" });
        var problem = await response.ReadJsonAsync();
        var errors = problem.GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal("Enter a subject.", errors.GetProperty("subject")[0].GetString());
        Assert.Equal("Choose Low, Medium or High.", errors.GetProperty("priority")[0].GetString());
        Assert.Equal(1, errors.GetProperty("requesterEmail").GetArrayLength());
    }

    [Fact]
    public async Task Post_MalformedJson_Returns400Problem_WithoutLeakingInternals()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(Base, content);
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.malformed", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("Exception", problem.ToString());
    }

    // ---- Changing (optimistic concurrency) -----------------------------------------------------

    [Fact]
    public async Task Patch_WithoutIfMatch_Returns428()
    {
        var created = await CreateAsync();

        var response = await _client.PatchAsJsonAsync($"{Base}/{created.Id}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WithCurrentETag_Succeeds_AndReturnsNextETag()
    {
        var created = await CreateAsync();

        var response = await PatchStatusAsync(created.Id, "InProgress", "\"v1\"");
        var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"v2\"", response.Headers.ETag!.Tag);
        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.Equal(2, body.GetProperty("history").GetArrayLength());
    }

    [Fact]
    public async Task Patch_WithStaleETag_Returns412()
    {
        var created = await CreateAsync();
        await PatchStatusAsync(created.Id, "InProgress", "\"v1\"");

        var response = await PatchStatusAsync(created.Id, "Resolved", "\"v1\"");
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("service_request.concurrency_conflict", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Patch_InvalidTransition_Returns409()
    {
        var created = await CreateAsync();

        var response = await PatchStatusAsync(created.Id, "Resolved", "\"v1\"");
        var problem = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("service_request.invalid_status_transition", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_WithCurrentETag_Returns204_ThenGetReturns404()
    {
        var created = await CreateAsync();

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"{Base}/{created.Id}");
        delete.Headers.TryAddWithoutValidation("If-Match", "\"v1\"");
        var deleted = await _client.SendAsync(delete);
        var afterwards = await _client.GetAsync($"{Base}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    // ---- Metadata & summary --------------------------------------------------------------------

    [Fact]
    public async Task Metadata_PublishesLimitsAndLabels()
    {
        var body = await (await _client.GetAsync("/api/v1/metadata")).ReadJsonAsync();

        Assert.Equal(2000, body.GetProperty("limits").GetProperty("descriptionMaxLength").GetInt32());
        Assert.Contains(body.GetProperty("statuses").EnumerateArray(), s => s.GetProperty("label").GetString() == "In progress");
    }

    [Fact]
    public async Task Summary_CountsAllStatuses()
    {
        var body = await (await _client.GetAsync($"{Base}/summary")).ReadJsonAsync();

        Assert.Equal(4, body.GetProperty("byStatus").GetArrayLength());
        Assert.Equal(
            body.GetProperty("total").GetInt32(),
            body.GetProperty("byStatus").EnumerateArray().Sum(s => s.GetProperty("count").GetInt32()));
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private sealed record Created(int Id);

    private async Task<Created> CreateAsync()
    {
        var response = await _client.PostAsJsonAsync(Base, Requests.Valid());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>(Json.Options))!;
    }

    private Task<HttpResponseMessage> PatchStatusAsync(int id, string status, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{Base}/{id}/status")
        {
            Content = JsonContent.Create(new { status })
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return _client.SendAsync(request);
    }
}
