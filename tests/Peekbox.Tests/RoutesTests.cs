using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Peekbox.Tests;

// Every page has one address, named on its action (SiteLinks); there is no "{controller}/{action}/{id?}" route.
[Collection(DatabaseCollection.Name)]
public class RoutesTests(PeekboxFixture fixture)
{
    private HttpClient Client(WebApplicationFactory<Program>? factory = null) =>
        (factory ?? fixture.Factory).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    // Written out on purpose: a new page must be added here, so a second address for a page cannot slip in unseen.
    private static readonly string[] Expected =
    [
        "/ GET,HEAD",
        "/builder GET,HEAD",
        "/builder POST",
        "/builder/packaging POST",
        "/builder/quantity POST",
        "/builder/review GET,HEAD",
        "/cart GET,HEAD",
        "/cart/add POST",
        "/cart/clear POST",
        "/checkout GET,HEAD",
        "/checkout POST",
        "/checkout/soldout GET",
        "/error any",
        "/items/{slug} GET,HEAD",
        "/orders/{token} GET,HEAD",
        "/track GET,HEAD",
        "/track POST",
    ];

    // Whether one of the addresses above takes this method. Used by the well-formed check on every page, so a form
    // that sends to a mistyped or moved address fails a test.
    internal static bool Answers(string path, string method) => Expected.Any(e =>
    {
        var (pattern, methods) = (e[..e.IndexOf(' ')], e[(e.IndexOf(' ') + 1)..]);
        var matches = System.Text.RegularExpressions.Regex.IsMatch(path, "^" + System.Text.RegularExpressions.Regex.Replace(pattern, @"\{[^}]+\}", "[^/]+") + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return matches && (methods == "any" || methods.Split(',').Contains(method));
    });

    [Fact]
    public void The_app_has_exactly_these_addresses()
    {
        // One endpoint per [HttpGet] and [HttpHead]: grouped by the C# method, so "GET,HEAD" is one address of one action.
        var routes = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Endpoint: e, Action: e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()))
            .Where(e => e.Action is not null)
            .GroupBy(e => (Pattern: "/" + e.Endpoint.RoutePattern.RawText!.TrimStart('/'), e.Action!.MethodInfo))
            .Select(g => g.Key.Pattern + " " + (g.Any(e => e.Endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is null)
                ? "any"
                : string.Join(",", g.SelectMany(e => e.Endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods).Order(StringComparer.Ordinal))))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Expected.Order(StringComparer.Ordinal), routes);
    }

    // What the old default route used to answer as well (a 200 for each).
    [Theory]
    [InlineData("/Home/Index")]
    [InlineData("/Home/Error")]
    [InlineData("/Track/Index")]
    [InlineData("/track/index/123")]
    [InlineData("/Cart/Index/x")]
    public async Task An_address_of_the_old_default_route_is_a_404(string path)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task The_error_page_opened_directly_is_a_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync("/error")).StatusCode);
    }

    // Production, with a database that cannot be reached: the item page fails, and the visitor gets our page, not the
    // framework's details and not a blank page.
    [Fact]
    public async Task An_unexpected_error_shows_the_error_page_in_production()
    {
        await using var broken = fixture.Factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2"));

        var response = await Client(broken).GetAsync("/items/tra-sen");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<h1>Có lỗi xảy ra</h1>", html);
        Assert.DoesNotContain("Npgsql", html);
    }
}
