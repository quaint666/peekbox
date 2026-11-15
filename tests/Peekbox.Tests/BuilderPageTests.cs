using System.Net;
using System.Text.RegularExpressions;

namespace Peekbox.Tests;

[Collection(DatabaseCollection.Name)]
public class BuilderPageTests(PeekboxFixture fixture)
{
    private async Task<string> GetBuilderAsync()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/builder");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Razor encodes Vietnamese letters as entities; decode them to compare text.
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static string FormIn(string html)
    {
        var form = Regex.Match(html, @"<form class=""card budget-card""[^>]*>([\s\S]*?)</form>");
        Assert.True(form.Success, "no budget form on the page");
        return form.Groups[1].Value;
    }

    [Fact]
    public async Task Builder_has_the_title_the_shared_header_and_footer_and_marks_its_menu_link()
    {
        var html = await GetBuilderAsync();

        Assert.Contains("<html lang=\"vi\">", html);
        Assert.Contains("<footer class=\"site-footer\">", html);
        Assert.Contains("<h1 class=\"builder__title\">Tạo hộp quà của bạn</h1>", html);
        Assert.Matches(@"<a href=""/builder"" aria-current=""page"">Tạo hộp quà</a>", html);
    }

    [Fact]
    public async Task The_step_strip_has_the_four_labels_in_order_and_the_first_is_the_current_step()
    {
        var html = await GetBuilderAsync();

        var steps = Regex.Matches(html, @"<li class=""builder-step""([^>]*)><span class=""builder-step__num"">(\d)</span><span>([^<]*)</span></li>")
            .Select(m => (Current: m.Groups[1].Value, Number: m.Groups[2].Value, Label: m.Groups[3].Value)).ToList();

        Assert.Equal(["1 Ngân sách", "2 Chọn món", "3 Đóng gói", "4 Xem lại"], steps.Select(s => $"{s.Number} {s.Label}"));
        Assert.Contains("aria-current=\"step\"", steps[0].Current);
        Assert.All(steps.Skip(1), s => Assert.DoesNotContain("aria-current", s.Current));
    }

    [Fact]
    public async Task The_quick_amounts_are_the_four_from_the_spec_and_each_is_a_submit_button()
    {
        var form = FormIn(await GetBuilderAsync());

        // Written out here on purpose: a test that read the same list would agree with a typo in it.
        var amounts = Regex.Matches(form, @"<button type=""submit"" class=""btn btn--outline btn--chip"" name=""quickAmount"" value=""(\d+)"">([^<]*)</button>")
            .Select(m => (Value: m.Groups[1].Value, Label: m.Groups[2].Value)).ToList();

        Assert.Equal(
            [("300000", "300.000đ"), ("500000", "500.000đ"), ("800000", "800.000đ"), ("1000000", "1.000.000đ")],
            amounts);
    }

    [Fact]
    public async Task The_card_is_a_post_form_with_an_empty_budget_and_date_and_an_anti_forgery_token()
    {
        var html = await GetBuilderAsync();
        var form = FormIn(html);

        Assert.Contains("<form class=\"card budget-card\" method=\"post\">", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Matches(@"<input class=""input"" id=""budget"" name=""budget"" type=""text"" inputmode=""numeric"" autocomplete=""off""\s*/?>", form);
        Assert.Matches(@"<input class=""input"" id=""delivery-date"" name=""deliveryDate"" type=""date""\s*/?>", form);
        Assert.Matches(@"<label class=""field__label"" for=""budget"">Ngân sách của bạn</label>", form);
        Assert.Matches(@"<label class=""field__label"" for=""delivery-date"">Ngày giao hàng</label>", form);
    }

    [Fact]
    public async Task Pressing_Enter_in_a_field_means_Cap_nhat_not_the_first_quick_amount()
    {
        // The browser presses the first submit button of the form. It must be the hidden "Cập nhật", before any quick amount.
        var form = FormIn(await GetBuilderAsync());

        var firstButton = Regex.Match(form, @"<button[^>]*>[^<]*</button>").Value;

        Assert.Contains("visually-hidden", firstButton);
        Assert.Contains("Cập nhật", firstButton);
        Assert.DoesNotContain("quickAmount", firstButton);
    }
}
