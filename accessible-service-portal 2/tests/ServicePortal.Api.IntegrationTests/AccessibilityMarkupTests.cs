using System.Net;

namespace ServicePortal.Api.IntegrationTests;

/// <summary>
/// Regression guards for accessibility essentials in the served page and assets.
/// They complement — never replace — axe scans (tests/e2e) and manual screen-reader testing.
/// </summary>
public sealed class AccessibilityMarkupTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("<html lang=\"en\">", "3.1.1 Language of Page")]
    [InlineData("class=\"skip-link\" href=\"#main\"", "2.4.1 Bypass Blocks")]
    [InlineData("<main id=\"main\" tabindex=\"-1\">", "2.4.1 skip target can receive focus")]
    [InlineData("<nav aria-label=\"Primary\">", "1.3.1 labelled navigation landmark")]
    [InlineData("role=\"status\" aria-live=\"polite\"", "4.1.3 polite status messages")]
    [InlineData("role=\"alert\" aria-live=\"assertive\"", "4.1.3 assertive error messages")]
    [InlineData("<label for=\"subject\">", "3.3.2 visible labels")]
    [InlineData("autocomplete=\"email\"", "1.3.5 Identify Input Purpose")]
    [InlineData("<fieldset id=\"priority\"", "1.3.1 grouped radio buttons")]
    [InlineData("<caption", "1.3.1 table caption")]
    [InlineData("aria-sort=\"descending\"", "4.1.2 sort state exposed")]
    [InlineData("aria-label=\"Pagination\"", "1.3.1 labelled pagination")]
    [InlineData("aria-expanded=\"false\"", "4.1.2 disclosure state")]
    [InlineData("<dialog id=\"details-dialog\"", "2.1.2 native modal dialog")]
    public async Task HomePage_ContainsAccessibilityFeature(string markup, string criterion)
    {
        var html = await _client.GetStringAsync("/");

        Assert.True(html.Contains(markup, StringComparison.Ordinal), $"Missing markup for WCAG {criterion}: {markup}");
    }

    [Fact]
    public async Task HomePage_HasExactlyOneH1_AndAllowsZoom()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Equal(1, html.Split("<h1").Length - 1);
        Assert.DoesNotContain("user-scalable=no", html);
        Assert.DoesNotContain("maximum-scale", html);
    }

    [Fact]
    public async Task HomePage_HasNoInlineScriptsOrStyles_SoTheStrictCspHolds()
    {
        var html = await _client.GetStringAsync("/");

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain(" style=\"", html);
        Assert.DoesNotContain(" onclick=", html);
    }

    [Theory]
    [InlineData("/css/app.css", "prefers-reduced-motion")]
    [InlineData("/css/app.css", "forced-colors")]
    [InlineData("/css/app.css", ":focus-visible")]
    [InlineData("/js/core/announcer.js", "#announcer-polite")]
    [InlineData("/js/core/dialog.js", "showModal")]
    public async Task Assets_AreServed_AndIncludeAccessibilitySupport(string path, string expected)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }
}
