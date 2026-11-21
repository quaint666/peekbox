using System.Net;

namespace Peekbox.Tests;

// The "page not found" page (F09): every unknown address shows it, with the 404 status.
[Collection(DatabaseCollection.Name)]
public class NotFoundPageTests(PeekboxFixture fixture)
{
    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/dip/no-such-occasion/extra")]
    [InlineData("/not-found")]
    public async Task An_unknown_address_shows_the_page_with_a_404(string path)
    {
        var response = await fixture.Client().GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Không tìm thấy trang này", html);
        Assert.Contains("href=\"/\"", html);
        Assert.Contains("href=\"/builder\"", html);
        Assert.Contains("Cần giúp?", html);
    }

    [Fact]
    public async Task A_wrong_method_keeps_its_own_status_and_gets_no_page()
    {
        var response = await fixture.Client().PostAsync("/healthz", new StringContent(""));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.DoesNotContain("Không tìm thấy", body);
    }
}
