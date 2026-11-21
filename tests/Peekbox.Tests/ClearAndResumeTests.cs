using System.Net;
using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// F13 and the clear button: a visitor who left a hamper with items is offered "Tiếp tục" or "Làm lại" on the home page, and
// the builder's box can be emptied (after a question), as the cart's can. Clearing keeps the budget and the date.
[Collection(DatabaseCollection.Name)]
public class ClearAndResumeTests(PeekboxFixture fixture)
{
    private static string Date => TestDates.TodayInVietnam.AddDays(20).ToString("yyyy-MM-dd");

    private async Task<Visitor> WithBoxAsync(bool items = true)
    {
        var visitor = new Visitor(fixture.Client());
        await visitor.PostAsync("/builder", new() { ["budget"] = "500000", ["deliveryDate"] = Date });
        if (items)
        {
            await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen", ["quantity"] = "1" });
        }

        return visitor;
    }

    private static string Banner(string html) => Regex.Match(html, @"<section class=""resume""[\s\S]*?</section>").Value;

    [Fact]
    public async Task A_visitor_with_items_in_the_box_is_offered_to_continue_or_start_over_on_the_home_page()
    {
        var visitor = await WithBoxAsync();

        var banner = Banner(await visitor.PageAsync("/"));

        Assert.Contains("Bạn đang có một hộp quà dở dang.", banner);
        Assert.Matches(@"1 món · [\d.]+đ", banner);
        Assert.Contains("<a class=\"btn btn--primary\" href=\"/builder\">Tiếp tục</a>", banner);
        Assert.Contains("popovertarget=\"lam-lai\">Làm lại</button>", banner);
        Assert.Contains("<input type=\"hidden\" name=\"from\" value=\"builder\">", banner);   // "Xóa" goes on building
    }

    [Fact]
    public async Task Nobody_else_sees_the_offer_a_new_visitor_a_visitor_with_only_a_budget_or_an_emptied_box()
    {
        Assert.Empty(Banner(await new Visitor(fixture.Client()).PageAsync("/")));
        Assert.Empty(Banner(await (await WithBoxAsync(items: false)).PageAsync("/")));

        var emptied = await WithBoxAsync();
        await emptied.PostAsync("/cart/clear", []);
        Assert.Empty(Banner(await emptied.PageAsync("/")));
    }

    [Fact]
    public async Task Clearing_from_the_builder_goes_back_to_the_items_and_keeps_the_budget_and_the_date()
    {
        var visitor = await WithBoxAsync();

        var response = await visitor.PostAsync("/cart/clear", new() { ["from"] = "builder" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/builder#chon-mon", response.Headers.Location?.OriginalString);
        var page = await visitor.PageAsync("/builder");
        Assert.DoesNotContain("Xóa hộp", page);                 // an empty box has nothing to clear
        Assert.Contains("500.000", page);                      // the budget stayed
        Assert.Contains(TestDates.TodayInVietnam.AddDays(20).ToString("dd/MM/yyyy"), page);   // and the date
    }

    [Fact]
    public async Task Clearing_from_the_cart_still_stays_in_the_cart_and_a_made_up_place_is_a_400()
    {
        var visitor = await WithBoxAsync();

        Assert.Equal("/cart", (await visitor.PostAsync("/cart/clear", [])).Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync("/cart/clear", new() { ["from"] = "nowhere" })).StatusCode);
    }

    [Fact]
    public async Task The_builders_box_has_a_clear_button_with_a_question_only_when_it_holds_an_item()
    {
        var withItem = await (await WithBoxAsync()).PageAsync("/builder");
        Assert.Contains("popovertarget=\"xoa-hop-builder\">Xóa hộp</button>", withItem);
        Assert.Contains("id=\"xoa-hop-builder\" popover", withItem);
        Assert.Contains("<input type=\"hidden\" name=\"from\" value=\"builder\">", withItem);

        var empty = await (await WithBoxAsync(items: false)).PageAsync("/builder");
        Assert.DoesNotContain("xoa-hop-builder", empty);
    }

    [Fact]
    public async Task The_cart_keeps_its_own_question_and_stays_on_the_cart()
    {
        var cart = await (await WithBoxAsync()).PageAsync("/cart");

        Assert.Contains("popovertarget=\"xoa-hop\">Xóa hộp</button>", cart);
        Assert.Contains("<input type=\"hidden\" name=\"from\" value=\"cart\">", cart);
    }
}
