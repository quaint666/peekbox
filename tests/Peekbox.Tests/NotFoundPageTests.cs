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

    // A form whose token has expired (a deploy, a day left open) is a 400 with no body of its own; it gets this page instead of
    // the browser's blank one. The same for an address with a value that cannot be read.
    [Fact]
    public async Task A_form_without_its_token_gets_the_request_not_valid_page_with_a_400()
    {
        var response = await fixture.Client().PostAsync("/track", new FormUrlEncodedContent(new Dictionary<string, string> { ["number"] = "PEEK000001", ["phone"] = "0900000000" }));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Yêu cầu không hợp lệ", html);
        Assert.Contains("tải lại trang", html);
        Assert.Contains("href=\"/\"", html);
        Assert.Contains("href=\"/builder\"", html);
        Assert.DoesNotContain("Không tìm thấy", html);
    }

    [Fact]
    public async Task An_address_with_a_value_that_cannot_be_read_gets_the_same_page()
    {
        var response = await fixture.Client().GetAsync("/builder?inDate=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Yêu cầu không hợp lệ", await response.Content.ReadAsStringAsync());
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
