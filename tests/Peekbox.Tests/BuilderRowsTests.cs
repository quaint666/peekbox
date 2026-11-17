using System.Net;
using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The builder's item list as rows, the cards in the box, and "Hộp gợi ý" (decisions 2026-11-17). Addresses and text are
// written out on purpose.
[Collection(DatabaseCollection.Name)]
public class BuilderRowsTests(PeekboxFixture fixture)
{
    private static readonly DateOnly Delivery = new(2027, 1, 15);

    private async Task<Visitor> SavedAsync(string budget)
    {
        var visitor = new Visitor(fixture.Client());
        var response = await visitor.PostAsync("/builder", new() { ["budget"] = budget, ["deliveryDate"] = Delivery.ToString("yyyy-MM-dd") });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return visitor;
    }

    private static List<string> Rows(string html) =>
        Regex.Matches(html, @"<section class=""item-row"" id=""hang-([^""]+)""").Select(m => m.Groups[1].Value).ToList();

    [Fact]
    public async Task Tat_ca_shows_one_row_per_category_in_panel_order_and_anything_else_shows_the_grid()
    {
        var visitor = await SavedAsync("500000");
        var html = await visitor.PageAsync("/builder");
        Assert.Equal(["banh", "tra-ca-phe", "hat-trai-cay-say", "mut-keo", "mat-ong-dac-san", "socola", "suc-khoe", "dac-san-man", "nhu-yeu-pham-tet", "do-uong", "qua-khong-an"], Rows(html));
        Assert.Contains("Hiện 81 món trong 11 danh mục.", html);
        Assert.DoesNotContain("<ul class=\"item-grid\">", html);

        // A row: its count, "Xem tất cả" and the end tile both open the category, the ← → for the script, the position.
        var banh = Regex.Match(html, @"<section class=""item-row"" id=""hang-banh""[\s\S]*?</section>").Value;
        Assert.Contains("Bánh <span class=\"item-row__count\">8 món</span>", banh);
        Assert.Equal(2, Regex.Matches(banh, @"href=""/builder\?category=banh#chon-mon""").Count);
        Assert.Contains("Xem tất cả Bánh", banh);
        Assert.Contains("<div class=\"row-buttons\" hidden>", banh);
        Assert.Equal(8, Regex.Matches(banh, @"<li class=""card item-card").Count);
        Assert.Contains("id=\"vuot-hint\" hidden>Vuốt sang trái để xem thêm món trong mỗi danh mục</p>", html);

        foreach (var query in new[] { "?category=banh", "?q=tra", "?sort=price-asc" })
        {
            var page = await visitor.PageAsync("/builder" + query);
            Assert.Empty(Rows(page));
            Assert.Contains("<ul class=\"item-grid\">", page);
        }
    }

    [Fact]
    public async Task A_card_in_the_box_is_marked_and_its_row_and_tile_count_it()
    {
        var visitor = await SavedAsync("500000");
        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen", ["quantity"] = "1" });
        var html = await visitor.PageAsync("/builder");

        var card = Regex.Match(html, @"<li class=""card item-card[^""]*"" id=""mon-tra-sen"">[\s\S]*?</h3>").Value;
        Assert.Contains("item-card--in-box", card);
        Assert.Contains("<span class=\"item-card__tag\">✓ Trong hộp</span>", card);
        Assert.DoesNotContain("Đủ hạn", card);   // "Trong hộp" takes the status's place
        Assert.DoesNotContain("item-card--in-box", Regex.Match(html, @"<li class=""card item-card[^""]*"" id=""mon-tra-o-long"">").Value);
        Assert.Contains("<span class=\"item-row__in-box\">✓ 1 trong hộp</span>", Regex.Match(html, @"id=""hang-tra-ca-phe-ten"">[\s\S]*?</h3>").Value);
        Assert.Contains("<span class=\"picker-tile__in-box\" aria-label=\"1 món trong hộp\">✓ 1</span>", html);
    }

    [Theory]
    [InlineData("500000", "", "500.000đ", new[] { "Hộp Sum Vầy", "Hộp Lộc Xuân", "Hộp Khỏe Xuân", "Hộp Ba Miền" })]
    [InlineData("650000", "", "500.000đ", new[] { "Hộp Sum Vầy", "Hộp Lộc Xuân", "Hộp Khỏe Xuân", "Hộp Ba Miền" })]
    [InlineData("500000", "20-11", "500.000đ", new[] { "Hộp Bục Giảng", "Hộp Mùa Thu" })]
    // 20/11 stops at 2 triệu: 5 triệu shows its 2 triệu hampers.
    [InlineData("5000000", "20-11", "2.000.000đ", new[] { "Hộp Trọn Năm" })]
    public async Task Hop_goi_y_shows_one_occasions_hampers_at_the_highest_budget_that_fits(string budget, string dip, string upTo, string[] names)
    {
        var visitor = await SavedAsync(budget);
        var html = await visitor.PageAsync("/builder" + (dip.Length > 0 ? "?dip=" + dip : ""));
        var strip = Regex.Match(html, @"<section class=""suggest""[\s\S]*?</section>").Value;

        Assert.Equal(names, Regex.Matches(strip, @"<h3 class=""suggest-card__name""><a [^>]*>([^<]+)</a>").Select(m => m.Groups[1].Value));
        Assert.Contains($"Hộp đến <strong>{upTo}</strong>, theo ngân sách của bạn.", strip);
        var chosen = Regex.Match(strip, @"<a class=""suggest__chip [^""]+"" href=""([^""]+)"" aria-current=""true"">([^<]+)</a>");
        Assert.Equal(dip.Length > 0 ? "20/11" : "Tết", chosen.Groups[2].Value);
        Assert.Contains($"<a href=\"/dip/{(dip.Length > 0 ? dip : "tet")}\">Xem mọi mức ngân sách</a>", strip);
        // The chips keep the list as it is; the default occasion stays out of the address.
        Assert.Contains("href=\"/builder?dip=20-11#goi-y\"", strip);
        Assert.Contains("href=\"/builder#goi-y\"", strip);
    }

    [Fact]
    public async Task Under_the_lowest_budget_there_is_no_strip()
    {
        var html = await (await SavedAsync("200000")).PageAsync("/builder");
        Assert.DoesNotContain("<section class=\"suggest\"", html);
    }

    [Fact]
    public async Task From_the_strip_an_empty_box_is_filled_and_a_full_one_asks_first_and_comes_back_to_the_strip()
    {
        var visitor = await SavedAsync("500000");
        var html = await visitor.PageAsync("/builder");
        Assert.Matches(@"<input type=""hidden"" name=""slug"" value=""sum-vay""> <input type=""hidden"" name=""from"" value=""builder"">", html);

        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-lai", ["quantity"] = "1" });
        html = await visitor.PageAsync("/builder");
        Assert.Contains("popovertarget=\"thay-sum-vay\">Dùng hộp này</button>", html);
        Assert.Contains("Hộp của bạn đang có 1 món. Dùng <strong>Hộp Sum Vầy</strong>", html);

        var response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "builder" });
        Assert.Equal("/builder#goi-y", response.Headers.Location?.OriginalString);
        response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "builder", ["replace"] = "true" });
        Assert.Equal("/builder#chon-mon", response.Headers.Location?.OriginalString);
    }
}
