using System.Net;
using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// "Hộp của bạn" as the phone's sheet, its thumbnails and count, "Vừa ngân sách" and "Bạn đang sửa Hộp …"
// (decisions 2026-11-17). Text and addresses are written out on purpose.
[Collection(DatabaseCollection.Name)]
public class BuilderBoxSheetTests(PeekboxFixture fixture)
{
    private static readonly DateOnly Delivery = new(2027, 1, 15);

    private async Task<Visitor> SavedAsync(string budget, params string[] slugs)
    {
        var visitor = new Visitor(fixture.Client());
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.PostAsync("/builder", new() { ["budget"] = budget, ["deliveryDate"] = Delivery.ToString("yyyy-MM-dd") })).StatusCode);
        foreach (var slug in slugs)
        {
            await visitor.PostAsync("/builder/quantity", new() { ["slug"] = slug, ["quantity"] = "1" });
        }

        return visitor;
    }

    private static string Panel(string html) => Regex.Match(html, @"<aside class=""card box-panel""[\s\S]*?</aside>").Value;

    [Fact]
    public async Task The_panel_is_a_popover_that_the_bar_opens_and_it_has_a_close_button()
    {
        var html = await (await SavedAsync("500000", "tra-sen", "hat-macca")).PageAsync("/builder");
        Assert.Contains("<aside class=\"card box-panel\" id=\"hop-cua-ban\" aria-labelledby=\"box-title\" popover>", html);
        Assert.Matches(@"<button type=""button"" class=""btn btn--outline box-bar__open"" popovertarget=""hop-cua-ban"">\s*<svg[\s\S]*?</svg>\s*Xem hộp \(2\)</button>", html);
        Assert.Contains("popovertarget=\"hop-cua-ban\" popovertargetaction=\"hide\" aria-label=\"Đóng\"", Panel(html));
    }

    [Fact]
    public async Task The_panel_counts_the_items_shows_a_thumbnail_per_line_and_a_one_line_sum_for_the_sheet()
    {
        var html = await (await SavedAsync("500000", "tra-sen", "hat-macca")).PageAsync("/builder");
        var panel = Panel(html);
        Assert.Contains("<span>Hộp của bạn</span><span class=\"box-panel__count\"> · 2 món</span></h2>", panel);
        Assert.Matches(@"<li class=""box-line"" id=""hop-tra-sen""> <img class=""box-line__img"" src=""/images/products/tra-sen\.webp""", panel);
        Assert.Contains("<p class=\"box-panel__compact\"><span>2 món · Còn lại <strong>130.000đ</strong></span><strong>370.000đ</strong></p>", panel);
        // The toast is there for the script, hidden, and kept from screen readers (they hear the status).
        Assert.Contains("<p class=\"box-toast\" id=\"box-toast\" aria-hidden=\"true\" popover=\"manual\"></p>", html);
        // An empty box: no count, no "Xem hộp".
        var empty = await (await SavedAsync("500000")).PageAsync("/builder");
        Assert.Contains("<h2 class=\"box-panel__title\" id=\"box-title\"><span>Hộp của bạn</span></h2>", empty);
        Assert.DoesNotContain("box-bar__open", empty);
    }

    // "Vừa ngân sách": only the items that cost no more than what is left (320.000đ after Trà sen in 500.000đ).
    [Fact]
    public async Task Vua_ngan_sach_shows_only_what_fits_what_is_left_and_the_list_keeps_it()
    {
        var visitor = await SavedAsync("500000", "tra-sen");
        var html = await visitor.PageAsync("/builder?fit=true&category=tra-ca-phe");
        Assert.Contains("<input type=\"checkbox\" id=\"fit-budget\" name=\"fit\" value=\"true\" checked=\"checked\">", html);
        Assert.Contains("Chỉ món vừa ngân sách còn lại <strong>320.000đ</strong>", html);
        var names = Regex.Matches(html, @"<a class=""item-card__link""[^>]*>([^<]+)</a>").Select(m => m.Groups[1].Value).ToList();
        Assert.Contains("Trà sen", names);                      // 180.000đ
        Assert.Contains("Cà phê Arabica Cầu Đất", names);       // 180.000đ
        Assert.DoesNotContain("Trà ô long hộp gỗ", names);      // 450.000đ
        Assert.DoesNotContain("Cà phê chồn", names);            // 650.000đ

        // The panel's links and the add forms keep it.
        Assert.Contains("href=\"/builder?category=socola&fit=true#chon-mon\"", html);
        Assert.Contains("action=\"/builder/quantity?category=tra-ca-phe&fit=true\"", html);
        var response = await visitor.PostAsync("/builder/quantity?category=tra-ca-phe&fit=true", new() { ["slug"] = "tra-lai", ["quantity"] = "1", ["category"] = "tra-ca-phe", ["fit"] = "true" });
        Assert.Equal("/builder?category=tra-ca-phe&fit=true#mon-tra-lai", response.Headers.Location?.OriginalString);

        // Without it, the 450.000đ tea is listed.
        Assert.Contains("Trà ô long hộp gỗ", await visitor.PageAsync("/builder?category=tra-ca-phe"));
    }

    [Fact]
    public async Task Ban_dang_sua_names_the_ready_hamper_until_the_box_is_emptied()
    {
        var visitor = await SavedAsync("500000");
        await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "tet" });
        const string note = "<p class=\"box-panel__from\">Bạn đang sửa <strong>Hộp Sum Vầy</strong>. Đổi món tùy ý; hạn dùng vẫn được kiểm tra.</p>";
        Assert.Contains(note, await visitor.PageAsync("/builder"));

        // Changing an item keeps it; taking every item out forgets it, and a new item does not bring it back.
        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen", ["quantity"] = "2" });
        Assert.Contains(note, await visitor.PageAsync("/builder"));
        foreach (var slug in new[] { "tra-sen", "hat-dieu-rang-muoi", "mut-gung" })
        {
            await visitor.PostAsync("/builder/quantity", new() { ["slug"] = slug, ["quantity"] = "0" });
        }

        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-lai", ["quantity"] = "1" });
        Assert.DoesNotContain("box-panel__from", await visitor.PageAsync("/builder"));
    }

    [Fact]
    public async Task Xoa_hop_in_the_cart_also_forgets_the_ready_hamper()
    {
        var visitor = await SavedAsync("500000");
        await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "tet" });
        await visitor.PageAsync("/cart");
        await visitor.PostAsync("/cart/clear", new());
        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-lai", ["quantity"] = "1" });
        Assert.DoesNotContain("box-panel__from", await visitor.PageAsync("/builder"));
    }
}
