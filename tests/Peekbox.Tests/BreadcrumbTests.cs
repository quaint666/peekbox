using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Tests;

// Every page but the home page and the error pages has one breadcrumb, drawn by the layout from what the page declares
// (decision 2026-11-21): it starts at "Trang chủ", ends at the page itself (no link, aria-current), and every link in it
// is an address the site really has.
[Collection(DatabaseCollection.Name)]
public class BreadcrumbTests(PeekboxFixture fixture)
{
    private static readonly Regex Nav = new(@"<nav class=""breadcrumb""[^>]*>[\s\S]*?</nav>");

    // The steps of the trail as written: a link is "label>/href", the page itself is "label|".
    private static List<string> Steps(string html)
    {
        var nav = Nav.Match(html).Value;
        return Regex.Matches(nav, @"<li>(?:<a href=""(?<href>[^""]*)"">(?<link>[^<]*)</a>|<span aria-current=""page"">(?<here>[^<]*)</span>)</li>")
            .Select(m => m.Groups["here"].Success ? WebUtility.HtmlDecode(m.Groups["here"].Value) + "|" : WebUtility.HtmlDecode(m.Groups["link"].Value) + ">" + WebUtility.HtmlDecode(m.Groups["href"].Value))
            .ToList();
    }

    private async Task<Visitor> WithBoxAsync()
    {
        var visitor = new Visitor(fixture.Client());
        var date = TestDates.TodayInVietnam.AddDays(20).ToString("yyyy-MM-dd");
        await visitor.PostAsync("/builder", new() { ["budget"] = "500000", ["deliveryDate"] = date });
        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen", ["quantity"] = "1" });
        return visitor;
    }

    private async Task<string> CreateOrderTokenAsync() => await fixture.WithDbAsync(async db =>
    {
        var ward = await db.Wards.OrderBy(w => w.SortOrder).FirstAsync();
        var product = await db.Products.OrderBy(p => p.Id).FirstAsync();
        var order = new Order
        {
            Token = Guid.NewGuid().ToString("N"), SenderName = "A", SenderPhone = "0900000000", SenderEmail = "a@example.com",
            RecipientName = "B", RecipientPhone = "0900111111", WardId = ward.Id, WardName = ward.Name, Street = "12 đường mẫu",
            DeliveryDate = new DateOnly(2027, 1, 15), DeliveryWindow = DeliveryWindow.Morning, GoodsVnd = 100_000,
            PackagingName = "Hộp giấy kraft", PackagingFeeVnd = 0, DeliveryFeeVnd = 0, TotalVnd = 100_000, CreatedAt = DateTimeOffset.UtcNow,
            Items = [new OrderItem { ProductId = product.Id, Name = "x", Brand = "y", Origin = "z", WeightGrams = 100, ExpiryDate = new DateOnly(2028, 1, 1), UnitPriceVnd = 100_000, Quantity = 1 }],
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Token;
    });

    [Fact]
    public async Task Every_page_has_its_trail_and_the_trail_is_well_formed()
    {
        var visitor = await WithBoxAsync();
        var order = await CreateOrderTokenAsync();
        var postSlug = (await new Visitor(fixture.Client()).PageAsync("/blog")) is var list ? Regex.Match(list, @"href=""/blog/([^""]+)""").Groups[1].Value : "";

        var pages = new (string Path, string[] Trail)[]
        {
            ("/blog", ["Trang chủ>/", "Blog|"]),
            ($"/blog/{postSlug}", ["Trang chủ>/", "Blog>/blog", "*|"]),
            ("/builder", ["Trang chủ>/", "Tạo hộp quà|"]),
            ("/builder/review", ["Trang chủ>/", "Tạo hộp quà>/builder", "Xem lại hộp quà|"]),
            ("/cart", ["Trang chủ>/", "Giỏ hàng|"]),
            ("/checkout", ["Trang chủ>/", "Giỏ hàng>/cart", "Thanh toán|"]),
            ($"/orders/{order}", ["Trang chủ>/", "Tra cứu đơn hàng>/track", "*|"]),
            ("/track", ["Trang chủ>/", "Tra cứu đơn hàng|"]),
            ("/about", ["Trang chủ>/", "Giới thiệu|"]),
            ("/legal", ["Trang chủ>/", "Điều khoản và bảo mật|"]),
            ("/dip/trung-thu", ["Trang chủ>/", "Trung thu|"]),
            ("/hop/an-khang", ["Trang chủ>/", "Tết>/dip/tet", "Hộp An Khang|"]),
            ("/hop/an-khang?from=builder", ["Trang chủ>/", "Tạo hộp quà>/builder#goi-y", "Hộp An Khang|"]),
            ("/items/tra-sen", ["Trang chủ>/", "Tạo hộp quà>/builder", "*>/builder?category=tra-ca-phe#chon-mon", "Trà sen|"]),
        };

        foreach (var (path, expected) in pages)
        {
            var response = await visitor.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} answered {(int)response.StatusCode}");
            var html = await visitor.TextAsync(response);
            Assert.True(Nav.Matches(html).Count == 1, $"{path} has {Nav.Matches(html).Count} breadcrumbs, not one");

            var steps = Steps(html);
            Assert.True(steps.Count == expected.Length, $"{path}: {string.Join(" / ", steps)}");
            for (var i = 0; i < expected.Length; i++)
            {
                // "*" stands for a label the test does not hard-code (a post's title, an order's number, a category).
                Assert.True(expected[i].StartsWith('*') ? steps[i].Contains(expected[i][1..]) : steps[i] == expected[i], $"{path} step {i + 1}: {steps[i]} (expected {expected[i]})");
            }

            // Only the last step is the page itself; the rest are links to addresses the site has.
            Assert.EndsWith("|", steps[^1]);
            Assert.All(steps[..^1], step => Assert.DoesNotContain("|", step));
            foreach (var link in steps[..^1].Select(s => s[(s.IndexOf('>') + 1)..]))
            {
                var address = link.Split('#')[0].Split('?')[0];
                Assert.True(RoutesTests.Answers(address, "GET"), $"{path}: the trail links to {link}, which is not an address of the site");
            }
        }
    }

    [Fact]
    public async Task The_home_page_and_the_error_pages_have_no_trail()
    {
        var visitor = new Visitor(fixture.Client());
        Assert.Empty(Nav.Matches(await visitor.PageAsync("/")));

        var missing = await visitor.GetAsync("/no-such-page");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(Nav.Matches(await visitor.TextAsync(missing)));
    }

    [Fact]
    public async Task The_old_trails_are_gone()
    {
        var visitor = new Visitor(fixture.Client());
        Assert.DoesNotContain("occ-bar", await visitor.PageAsync("/dip/tet"));
        Assert.DoesNotContain("occ-crumbs", await visitor.PageAsync("/dip/tet"));
    }
}
