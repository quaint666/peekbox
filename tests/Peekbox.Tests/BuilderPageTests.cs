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

    // "Chọn nhanh" (decision 2026-11-17): a button on the budget field's line that opens the seven amounts. Each is a
    // link that only fills the field (/builder?chon=…); nothing in the panel sends the form.
    [Fact]
    public async Task Chon_nhanh_opens_the_seven_amounts_as_links_that_fill_the_field()
    {
        var html = await GetBuilderAsync();
        Assert.Contains("<button type=\"button\" class=\"btn btn--outline picker-button\" popovertarget=\"chon-nhanh\">", FormIn(html));

        // Written out here on purpose: a test that read the same list would agree with a typo in it.
        var amounts = Regex.Matches(html, @"<a class=""picker-tile picker-tile--amount"" href=""/builder\?chon=(\d+)#buoc-1""[^>]*data-amount=""([^""]+)""[^>]*>([^<]*)</a>")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)).ToList();
        Assert.Equal(
            [("300000", "300.000đ", "300.000đ"), ("500000", "500.000đ", "500.000đ"), ("800000", "800.000đ", "800.000đ"), ("1000000", "1.000.000đ", "1 triệu"),
             ("2000000", "2.000.000đ", "2 triệu"), ("5000000", "5.000.000đ", "5 triệu"), ("10000000", "10.000.000đ", "10 triệu")],
            amounts);
        Assert.Contains("Bấm một mức để điền vào ô ngân sách. Ngân sách chỉ đổi khi bạn bấm Lưu.", html);
    }

    [Fact]
    public async Task The_card_is_a_post_form_with_an_empty_budget_and_date_and_an_anti_forgery_token()
    {
        var html = await GetBuilderAsync();
        var form = FormIn(html);

        Assert.Contains("<form class=\"card budget-card\" id=\"buoc-1\" method=\"post\" action=\"/builder\">", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Matches(@"<input class=""input"" id=""budget"" name=""budget"" type=""text"" inputmode=""numeric"" autocomplete=""off"" value=""""\s*/?>", form);
        Assert.Matches(@"<input class=""input"" id=""delivery-date"" name=""deliveryDate"" type=""date"" value=""""\s*/?>", form);
        Assert.Matches(@"<label class=""field__label"" for=""budget"">Ngân sách của bạn</label>", form);
        Assert.Matches(@"<label class=""field__label"" for=""delivery-date"">Ngày giao hàng</label>", form);
    }

    [Fact]
    public async Task Pressing_Enter_in_a_field_means_Luu_and_the_form_has_no_other_submit_button()
    {
        // The browser presses the first submit button of the form: "Lưu". "Chọn nhanh" is a plain button.
        var form = FormIn(await GetBuilderAsync());

        var submits = Regex.Matches(form, @"<button type=""submit""[^>]*>([^<]*)</button>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Lưu"], submits);
        Assert.Equal(Regex.Matches(form, "<button ").Count, submits.Count + Regex.Matches(form, @"<button type=""button""").Count);
    }
}
