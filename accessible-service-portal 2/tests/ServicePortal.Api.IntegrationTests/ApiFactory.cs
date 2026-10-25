using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ServicePortal.Api.IntegrationTests;

/// <summary>
/// Hosts the real application in-process with the "Testing" environment
/// (in-memory storage with sample data, generous rate limits — see appsettings.Testing.json).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
}

/// <summary>A factory with a tiny submission limit, for exercising rate limiting.</summary>
public sealed class RateLimitedApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:SubmissionPermitLimit", "2");
        builder.UseSetting("RateLimiting:SubmissionWindowSeconds", "60");
    }
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Options);
}

internal static class Requests
{
    public static object Valid(string subject = "Keyboard trap in signing modal") => new
    {
        subject,
        category = "Accessibility barrier",
        priority = "High",
        description = "Focus cannot leave the signing modal using Tab or Escape in Edge.",
        requesterName = "Alex Morgan",
        requesterEmail = "alex.morgan@example.com",
        requesterPhone = (string?)null
    };
}
