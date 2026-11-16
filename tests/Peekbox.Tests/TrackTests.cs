using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Tests;

// The track page (F60): /track. An order's number and its sender's phone (0900000000, from TestOrders) lead to the
// order's page; anything else gets one message. The fixture allows plenty of lookups; one test runs its own app with a
// limit of 1.
[Collection(DatabaseCollection.Name)]
public class TrackTests(PeekboxFixture fixture)
{
    private const string NotFound = "<strong>Không tìm thấy đơn hàng.</strong> Mã đơn hàng và số điện thoại không khớp với đơn nào. Hãy kiểm tra lại cả hai.";

    private HttpClient Client(WebApplicationFactory<Program>? factory = null) =>
        (factory ?? fixture.Factory).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static Dictionary<string, string> Form(string number, string phone) => new() { ["number"] = number, ["phone"] = phone };

    // The alert of a page, as one string (empty when there is none).
    private static string Alert(string html) => Regex.Match(html, @"<div class=""track-alert[^""]*""[\s\S]*?</div>").Value;

    // An empty field comes back as null, and Razor leaves a null value attribute out.
    private static string Value(string typed) => typed.Length == 0 ? "" : $"value=\"{typed}\" ";

    private async Task<(string Token, int Id, Dictionary<string, int> Stock)> PlaceAsync()
    {
        var (token, _, stock) = await TestOrders.PlaceAsync(fixture, 1, PaymentMethod.CashOnDelivery);
        var id = await fixture.WithDbAsync(db => db.Orders.Where(o => o.Token == token).Select(o => o.Id).SingleAsync());
        return (token, id, stock);
    }

    [Fact]
    public async Task The_page_has_the_form_and_is_marked_in_the_menu()
    {
        var visitor = new Visitor(Client(), null);
        var html = await visitor.PageAsync("/track");

        Assert.Contains("<title>Tra cứu đơn hàng · Peekbox</title>", html);
        Assert.Contains("<a href=\"/track\" aria-current=\"page\">Tra cứu đơn hàng</a>", html);
        Assert.Contains("<h1 class=\"track__title\" id=\"track-title\">Tra cứu đơn hàng</h1>", html);
        Assert.Contains("<form class=\"track-form\" method=\"post\" action=\"/track\">", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("id=\"track-number\" name=\"number\" type=\"text\" placeholder=\"Ví dụ: PEEK000123\"", html);
        Assert.Contains("id=\"track-phone\" name=\"phone\" type=\"tel\" placeholder=\"0900 000 000\"", html);
        Assert.Contains("Mã có trên trang xác nhận đơn hàng.", html);
        var card = Regex.Match(html, @"<section class=""card track""[\s\S]*?</section>").Value;
        Assert.DoesNotContain("email xác nhận", card);   // no email is sent yet (F61)
        Assert.Contains("Cần giúp? [Số điện thoại hoặc email hỗ trợ] · Hỗ trợ [7:00 – 21:30]", html);
        Assert.Empty(Alert(html));
    }

    [Theory]
    [InlineData("{0}", "0900000000")]
    [InlineData("{1}", "0900 000 000")]
    [InlineData(" {0} ", "0900.000.000")]
    [InlineData("{2}", " 0900-000-000 ")]
    public async Task The_right_number_and_phone_lead_to_the_order_page(string numberShape, string phone)
    {
        var (token, id, stock) = await PlaceAsync();
        try
        {
            var number = string.Format(numberShape, OrderNumber.Format(id), OrderNumber.Format(id).ToLowerInvariant(), id);
            var visitor = new Visitor(Client(), null);
            await visitor.PageAsync("/track");

            var response = await visitor.PostAsync("/track", Form(number, phone));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal($"/orders/{token}", response.Headers.Location?.OriginalString);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task Any_mismatch_gets_the_same_message_and_keeps_what_was_typed()
    {
        var (_, id, stock) = await PlaceAsync();
        try
        {
            var right = OrderNumber.Format(id);
            (string Number, string Phone)[] tries =
            [
                (right, "0900000001"),                   // the right number, another phone
                (OrderNumber.Format(id + 1000), "0900000000"),   // a number with no order, the right phone
                ("PEEKabc<b>", "0900000000"),             // not a number at all
                (right, ""),                             // no phone
                ("", ""),                                // nothing
            ];
            var visitor = new Visitor(Client(), null);
            await visitor.PageAsync("/track");
            var alerts = new List<string>();
            foreach (var (number, phone) in tries)
            {
                var response = await visitor.PostAsync("/track", Form(number, phone));
                var html = await visitor.TextAsync(response);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Null(response.Headers.Location);
                Assert.Contains(NotFound, html);
                Assert.Contains($"name=\"number\" type=\"text\" {Value(number)}placeholder", html);
                Assert.Contains($"name=\"phone\" type=\"tel\" {Value(phone)}placeholder", html);
                alerts.Add(Alert(html));
            }

            Assert.Single(alerts.Distinct());
            Assert.Contains("role=\"alert\" tabindex=\"-1\" autofocus", alerts[0]);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task Too_many_lookups_get_the_wait_note_and_a_429_even_for_the_right_pair()
    {
        var (_, id, stock) = await PlaceAsync();
        try
        {
            await using var strict = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:TrackPer15Minutes", "1"));
            var visitor = new Visitor(Client(strict), null);
            await visitor.PageAsync("/track");

            var first = await visitor.PostAsync("/track", Form("PEEK999999999", "0900000000"));   // a miss, and it still counts
            var second = await visitor.PostAsync("/track", Form(OrderNumber.Format(id), "0900000000"));
            var html = await visitor.TextAsync(second);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
            Assert.Null(second.Headers.Location);
            Assert.Contains("<div class=\"track-alert track-alert--wait\" role=\"alert\" tabindex=\"-1\" autofocus>", html);
            Assert.Contains("<strong>Bạn đã thử quá nhiều lần.</strong> Hãy đợi vài phút rồi thử lại.", html);
            Assert.Contains($"value=\"{OrderNumber.Format(id)}\"", html);
            Assert.DoesNotContain("Không tìm thấy", html);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task A_lookup_without_the_form_token_is_refused()
    {
        var visitor = new Visitor(Client(), null);
        await visitor.PageAsync("/track");

        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync("/track", Form("PEEK000001", "0900000000"), withToken: false)).StatusCode);
    }

    [Fact]
    public async Task Every_state_of_the_track_page_is_well_formed()
    {
        var visitor = new Visitor(Client(), null);
        BuilderItemsTests.AssertWellFormed("empty", await (await visitor.GetAsync("/track")).Content.ReadAsStringAsync());
        BuilderItemsTests.AssertWellFormed("miss", await (await visitor.PostAsync("/track", Form("PEEK999999999", "0900000000"))).Content.ReadAsStringAsync());
        await using var strict = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:TrackPer15Minutes", "1"));
        var limited = new Visitor(Client(strict), null);
        await limited.PageAsync("/track");
        await limited.PostAsync("/track", Form("x", "y"));
        BuilderItemsTests.AssertWellFormed("wait", await (await limited.PostAsync("/track", Form("x", "y"))).Content.ReadAsStringAsync());
    }
}
