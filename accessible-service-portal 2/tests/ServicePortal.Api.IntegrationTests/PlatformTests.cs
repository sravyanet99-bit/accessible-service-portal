using System.Net;
using System.Net.Http.Json;

namespace ServicePortal.Api.IntegrationTests;

/// <summary>Cross-cutting behaviour: health, security headers, correlation ids, rate limiting.</summary>
public sealed class PlatformTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_ReportHealthy(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        var response = await _client.GetAsync("/");

        Assert.Contains("script-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task CorrelationId_IsEchoed_WhenSafe_AndGenerated_Otherwise()
    {
        using var safe = new HttpRequestMessage(HttpMethod.Get, "/api/v1/metadata");
        safe.Headers.Add("X-Correlation-ID", "support-ticket-42");
        using var unsafeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/metadata");
        unsafeRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", "bad value <script>");

        var echoed = (await _client.SendAsync(safe)).Headers.GetValues("X-Correlation-ID").Single();
        var generated = (await _client.SendAsync(unsafeRequest)).Headers.GetValues("X-Correlation-ID").Single();

        Assert.Equal("support-ticket-42", echoed);
        Assert.NotEqual("bad value <script>", generated);
        Assert.NotEmpty(generated);
    }

    [Fact]
    public async Task UnknownApiRoute_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

public sealed class RateLimitingTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Submissions_BeyondTheLimit_Return429_WithRetryAfter()
    {
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await client.PostAsJsonAsync("/api/v1/service-requests", Requests.Valid($"Rate limit probe {i}"));
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.TooManyRequests }, statuses);
        Assert.True(last!.Headers.Contains("Retry-After"));
        Assert.Equal("rate_limited", (await last.ReadJsonAsync()).GetProperty("code").GetString());
    }
}
