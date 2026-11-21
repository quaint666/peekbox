using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Security;

namespace Peekbox.Tests;

[Collection(DatabaseCollection.Name)]
public class SecurityHeaderTests(PeekboxFixture fixture)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/css/tokens.css")]
    [InlineData("/this-page-does-not-exist")]
    public async Task Every_response_carries_the_content_security_policy(string path)
    {
        var response = await fixture.Factory.CreateClient().GetAsync(path);

        Assert.True(response.Headers.TryGetValues(ContentSecurityPolicy.HeaderName, out var values), $"{path}: no policy header");
        Assert.Equal(ContentSecurityPolicy.Value, Assert.Single(values));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/css/tokens.css")]
    [InlineData("/this-page-does-not-exist")]
    public async Task Every_response_says_never_send_the_address_to_another_site(string path)
    {
        var response = await fixture.Factory.CreateClient().GetAsync(path);

        Assert.True(response.Headers.TryGetValues(ReferrerPolicy.HeaderName, out var values), $"{path}: no referrer policy header");
        Assert.Equal("no-referrer", Assert.Single(values));
    }

    // Typed out on purpose, as the policy below: a change to what the browser may do must show in this file's diff.
    [Theory]
    [InlineData("/")]
    [InlineData("/css/tokens.css")]
    [InlineData("/this-page-does-not-exist")]
    public async Task Every_response_says_never_guess_the_type_and_switches_off_what_the_site_never_uses(string path)
    {
        var response = await fixture.Factory.CreateClient().GetAsync(path);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("camera=(), microphone=(), geolocation=(), payment=(), usb=()", Assert.Single(response.Headers.GetValues("Permissions-Policy")));
    }

    [Fact]
    public void The_server_does_not_say_what_it_is()
    {
        var kestrel = fixture.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>>().Value;

        Assert.False(kestrel.AddServerHeader);
    }

    // The header shows the visitor's own cart count on every page, so no page may be kept by a shared cache for someone else.
    [Theory]
    [InlineData("/", "private, no-cache")]
    [InlineData("/dip/tet", null)]
    [InlineData("/items/tra-sen", null)]
    [InlineData("/track", null)]
    [InlineData("/builder", null)]
    [InlineData("/this-page-does-not-exist", null)]
    public async Task A_page_is_never_cached_for_anyone_but_the_visitor(string path, string? exactly)
    {
        var response = await fixture.Factory.CreateClient().GetAsync(path);
        var cache = response.Headers.NonValidated.TryGetValues("Cache-Control", out var values) ? values.ToString() : "";   // as sent: the typed header reorders it

        Assert.True(cache.Contains("private") || cache.Contains("no-store"), $"{path}: Cache-Control is \"{cache}\"");
        if (exactly is not null)
        {
            Assert.Equal(exactly, cache);
        }
    }

    [Fact]
    public async Task A_static_file_keeps_its_own_caching_and_is_not_made_private()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/css/tokens.css");

        Assert.DoesNotContain("private", response.Headers.CacheControl?.ToString() ?? "");
    }

    [Fact]
    public void The_policy_allows_exactly_these_sources_and_no_others()
    {
        // Typed out on purpose, a second copy of ContentSecurityPolicy.Value: widening the policy (another site,
        // 'unsafe-inline', a wildcard, a dropped directive) must show up in this file's diff, which the owner reviews.
        var expected = new Dictionary<string, string>
        {
            ["default-src"] = "'self'",
            ["script-src"] = "'self'",
            ["style-src"] = "'self'",
            ["img-src"] = "'self' data:",
            ["font-src"] = "'self'",
            ["connect-src"] = "'self'",
            ["object-src"] = "'none'",
            ["base-uri"] = "'self'",
            ["form-action"] = "'self'",
            ["frame-ancestors"] = "'none'",
        };

        var actual = ContentSecurityPolicy.Value
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(directive => directive.Split(' ', 2))
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : "");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void The_policy_blocks_inline_scripts_and_styles()
    {
        // A policy that allows 'unsafe-inline' would defeat the rule.
        Assert.DoesNotContain("unsafe-inline", ContentSecurityPolicy.Value);
        Assert.Contains("script-src 'self'", ContentSecurityPolicy.Value);
        Assert.Contains("style-src 'self'", ContentSecurityPolicy.Value);
    }

    [Fact]
    public void The_policy_stops_other_sites_from_framing_the_page()
    {
        Assert.Contains("frame-ancestors 'none'", ContentSecurityPolicy.Value);
    }
}
