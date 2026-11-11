using Microsoft.AspNetCore.Mvc.Testing;
using Peekbox.Web.Security;

namespace Peekbox.Tests;

public class SecurityHeaderTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/css/tokens.css")]
    [InlineData("/this-page-does-not-exist")]
    public async Task Every_response_carries_the_content_security_policy(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.True(response.Headers.TryGetValues(ContentSecurityPolicy.HeaderName, out var values), $"{path}: no policy header");
        Assert.Equal(ContentSecurityPolicy.Value, Assert.Single(values));
    }

    [Fact]
    public void The_policy_blocks_inline_scripts_and_styles()
    {
        // A policy that allows 'unsafe-inline' would defeat the rule.
        Assert.DoesNotContain("unsafe-inline", ContentSecurityPolicy.Value);
        Assert.Contains("script-src 'self'", ContentSecurityPolicy.Value);
        Assert.Contains("style-src 'self'", ContentSecurityPolicy.Value);
    }
}
