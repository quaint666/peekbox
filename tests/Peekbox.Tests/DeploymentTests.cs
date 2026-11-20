using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// What the site needs before it can run behind the staging proxies (docs/architecture.md, section 10): the visitor's
// address and "https" from X-Forwarded-*, the host name check, the health check and demo mode.
[Collection(DatabaseCollection.Name)]
public class DeploymentTests(PeekboxFixture fixture)
{
    // The staging chain: Cloudflare adds the visitor, nginx adds cloudflared (127.0.0.1) and says "https".
    private static string Chain(string visitor, string forged = "") => $"{forged}{visitor}, 127.0.0.1";

    private WebApplicationFactory<Program> Strict(string? hops) => fixture.Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("RateLimits:TrackPer15Minutes", "1");
        if (hops is not null)
        {
            builder.UseSetting("ForwardedHeaders:Hops", hops);
        }
    });

    private Visitor VisitorBehindProxy(WebApplicationFactory<Program> factory, string forwardedFor)
    {
        var client = fixture.Client(factory);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        return new Visitor(client, null);
    }

    private static Dictionary<string, string> Lookup() => new() { ["Number"] = "PEEK999999999", ["Phone"] = "0900000000" };

    // Each lookup counts toward a limit of 1 per address, so a 429 on the second one means "same address".
    [Fact]
    public async Task With_two_hops_each_visitor_in_the_forwarded_header_has_their_own_limit()
    {
        await using var factory = Strict("2");
        var first = VisitorBehindProxy(factory, Chain("1.1.1.1"));
        var second = VisitorBehindProxy(factory, Chain("2.2.2.2"));
        await first.PageAsync("/track");
        await second.PageAsync("/track");

        Assert.Equal(HttpStatusCode.OK, (await first.PostAsync("/track", Lookup())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsync("/track", Lookup())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await first.PostAsync("/track", Lookup())).StatusCode);
    }

    // A visitor can write anything at the left of the header; only the two entries the proxies added count.
    [Fact]
    public async Task A_made_up_address_at_the_left_of_the_header_does_not_give_a_visitor_a_new_limit()
    {
        await using var factory = Strict("2");
        var honest = VisitorBehindProxy(factory, Chain("1.1.1.1"));
        var forging = VisitorBehindProxy(factory, Chain("1.1.1.1", forged: "9.9.9.9, "));
        await honest.PageAsync("/track");
        await forging.PageAsync("/track");

        Assert.Equal(HttpStatusCode.OK, (await honest.PostAsync("/track", Lookup())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await forging.PostAsync("/track", Lookup())).StatusCode);
    }

    // The default: no hops configured, so a header from a visitor is not believed (a laptop copy, the tests).
    [Fact]
    public async Task Without_the_setting_the_forwarded_header_is_ignored()
    {
        await using var factory = Strict(null);
        var first = VisitorBehindProxy(factory, Chain("1.1.1.1"));
        var second = VisitorBehindProxy(factory, Chain("2.2.2.2"));
        await first.PageAsync("/track");
        await second.PageAsync("/track");

        Assert.Equal(HttpStatusCode.OK, (await first.PostAsync("/track", Lookup())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await second.PostAsync("/track", Lookup())).StatusCode);
    }

    [Fact]
    public async Task The_hamper_cookie_is_secure_when_the_proxy_says_https_and_not_otherwise()
    {
        await using var behind = Strict("2");
        var secure = await SetCookieAsync(VisitorBehindProxy(behind, Chain("1.1.1.1")));
        var plain = await SetCookieAsync(new Visitor(fixture.Client(), null));

        Assert.Contains("secure", secure);
        Assert.DoesNotContain("secure", plain);
    }

    private static async Task<string> SetCookieAsync(Visitor visitor)
    {
        var response = await visitor.PostAsync("/builder", new() { ["budget"] = "300000" });
        return Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("peekbox_hamper=", StringComparison.Ordinal)).ToLowerInvariant();
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("two")]
    public void A_hops_setting_that_is_not_a_whole_number_from_zero_stops_the_app_at_start(string hops)
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("ForwardedHeaders:Hops", hops));

        var error = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains("Hops", error.ToString(), StringComparison.Ordinal);
    }

    // AllowedHosts is set per environment (the staging name, and "localhost" for the container's own health check).
    [Fact]
    public async Task A_host_name_that_is_not_allowed_gets_a_400()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("AllowedHosts", "peekbox-staging.example;localhost"));
        var client = fixture.Client(factory);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/healthz", "peekbox-staging.example")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Get(client, "/healthz", "evil.example")).StatusCode);
    }

    private static Task<HttpResponseMessage> Get(HttpClient client, string path, string host)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = host;
        return client.SendAsync(request);
    }

    [Fact]
    public async Task The_health_check_says_200_when_the_database_answers()
    {
        var response = await fixture.Client().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_health_check_says_503_when_the_database_does_not_answer()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=2"));

        var response = await fixture.Client(factory).GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // Demo mode: the checkout is a notice, a form sent anyway makes no order, and nothing else changes.
    [Fact]
    public async Task In_demo_mode_the_checkout_shows_the_notice_and_takes_no_order()
    {
        await using var demo = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Demo:Enabled", "true"));
        var visitor = new Visitor(fixture.Client(demo), null);
        var before = await fixture.WithDbAsync(db => db.Orders.CountAsync());

        var page = await visitor.PageAsync("/checkout");
        var sent = await visitor.PostAsync("/checkout", new() { ["name"] = "Test", ["phone"] = "0900000000" });
        var afterPost = await visitor.TextAsync(sent);

        Assert.Contains("Đây là bản xem thử", page);
        Assert.DoesNotContain("<form", page);
        Assert.DoesNotContain("Đặt hàng", page);
        Assert.Contains("<meta name=\"robots\"", page);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.Contains("Đây là bản xem thử", afterPost);
        Assert.Equal(before, await fixture.WithDbAsync(db => db.Orders.CountAsync()));
        BuilderItemsTests.AssertWellFormed("demo checkout", await (await visitor.GetAsync("/checkout")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Without_demo_mode_a_visitor_with_no_box_still_goes_back_to_the_cart()
    {
        var response = await new Visitor(fixture.Client(), null).GetAsync("/checkout");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cart", response.Headers.Location?.OriginalString);
    }
}
