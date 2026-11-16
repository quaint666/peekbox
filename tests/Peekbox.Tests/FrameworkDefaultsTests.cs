using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// Two framework defaults that Program.cs sets on purpose (docs/architecture.md, section 7 and section 11).
[Collection(DatabaseCollection.Name)]
public class FrameworkDefaultsTests(PeekboxFixture fixture)
{
    // Every POST in the app. A new one is covered by the global filter without being listed here.
    [Theory]
    [InlineData("/builder")]
    [InlineData("/builder/quantity")]
    [InlineData("/builder/packaging")]
    [InlineData("/cart/add")]
    [InlineData("/cart/clear")]
    public async Task A_post_without_the_anti_forgery_token_is_refused(string path)
    {
        var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["quantity"] = "1" }));

        Assert.Equal(400, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/builder")]
    [InlineData("/cart")]
    public async Task A_get_needs_no_token(string path)
    {
        var response = await fixture.Factory.CreateClient().GetAsync(path);

        Assert.Equal(200, (int)response.StatusCode);
    }

    // A singleton that takes a scoped service would share one HamperService (and one DbContext) with every visitor.
    // Development refuses it by default; Program.cs makes Production refuse it too, at start.
    [Fact]
    public void A_singleton_that_takes_a_scoped_service_stops_the_app_in_production()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services => services.AddSingleton<HoldsAHamperService>());
        });

        var error = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains("scoped service", error.ToString(), StringComparison.Ordinal);
    }

    private sealed class HoldsAHamperService(HamperService hampers)
    {
        public HamperService Hampers { get; } = hampers;
    }
}
