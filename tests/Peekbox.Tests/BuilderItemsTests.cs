using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// The item list of /builder (F16, F18, F22, F23) with the 24 seed items. The expected names and counts are written out
// on purpose: a test that computed them with the same rule would agree with a mistake in it.
[Collection(DatabaseCollection.Name)]
public class BuilderItemsTests(PeekboxFixture fixture)
{
    // Delivery 15/01/2027: with the 7-day margin, Bánh quy bơ (10/01/2027) and Kẹo lạc (18/01/2027) are out of date.
    private static readonly DateOnly Delivery = new(2027, 1, 15);

    private HttpClient Client() => fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    // A visitor who already pressed "Lưu": a hamper row, found by its cookie.
    private async Task<string> SavedHamperAsync(DateOnly? deliveryDate)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(db =>
        {
            db.Hampers.Add(new Hamper { Token = token, BudgetVnd = 500_000, DeliveryDate = deliveryDate, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });
        return token;
    }

    private async Task<string> PageAsync(string? token, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/builder" + query);
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"{HamperService.CookieName}={token}");
        }

        var response = await Client().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static List<string> CardNames(string html) =>
        Regex.Matches(html, @"<h3 class=""item-card__name""><a class=""item-card__link"" href=""[^""]*"">([^<]*)</a></h3>").Select(m => m.Groups[1].Value).ToList();

    private static string CurrentStep(string html) =>
        Regex.Match(html, @"<li class=""builder-step"" aria-current=""step""><span class=""builder-step__num"">(\d)</span>").Groups[1].Value;

    [Fact]
    public async Task Without_a_saved_hamper_only_the_lock_note_shows_and_step_1_is_current()
    {
        var html = await PageAsync(token: null);

        Assert.Contains("Nhập ngân sách và ngày giao hàng rồi bấm <strong>Lưu</strong> để mở bước chọn món và đóng gói.", html);
        Assert.Empty(CardNames(html));
        Assert.DoesNotContain("item-filters", html);
        Assert.Equal("1", CurrentStep(html));
    }

    [Fact]
    public async Task A_saved_budget_without_a_date_keeps_the_items_locked()
    {
        var html = await PageAsync(await SavedHamperAsync(deliveryDate: null));

        Assert.Contains("class=\"lock-note\"", html);
        Assert.Empty(CardNames(html));
    }

    [Fact]
    public async Task With_a_saved_date_the_items_in_date_show_and_the_two_out_of_date_ones_are_hidden()
    {
        var html = await PageAsync(await SavedHamperAsync(Delivery));
        var names = CardNames(html);

        Assert.DoesNotContain("để mở bước chọn món và đóng gói", html);   // the items' lock note (packaging has its own)
        Assert.Equal(22, names.Count);
        Assert.DoesNotContain("Bánh quy bơ", names);
        Assert.DoesNotContain("Kẹo lạc", names);
        Assert.Contains("Hiện 22 món. 2 món hết hạn trước ngày giao đang được ẩn.", html);
        Assert.Matches(@"<a href=""/builder\?inDate=false#chon-mon"">Hiện cả 2 món</a>", html);
        Assert.Contains("Chỉ món đủ hạn cho ngày giao <strong>15/01/2027</strong>", html);
        Assert.Contains("HSD 12/2027 · đủ hạn", html);   // Trà sen, 31/12/2027
        Assert.Equal("2", CurrentStep(html));
        Assert.Contains("<li class=\"builder-step builder-step--done\"><span class=\"builder-step__num\">1</span>", html);
    }

    [Fact]
    public async Task Turning_the_checkbox_off_shows_all_24_with_an_amber_badge_on_the_two()
    {
        var token = await SavedHamperAsync(Delivery);
        var html = await PageAsync(token, "?inDate=false");

        Assert.Equal(24, CardNames(html).Count);
        Assert.Contains("Hiện 24 món.", html);
        Assert.DoesNotContain("đang được ẩn", html);
        Assert.Contains("HSD 10/01/2027, trước ngày giao", html);
        Assert.Contains("HSD 18/01/2027, trước ngày giao", html);
        Assert.DoesNotMatch(@"<input[^>]*id=""in-date""[^>]*checked", html);

        // A ticked checkbox sends "true" and its hidden "false" (the framework's checkbox); the first value wins.
        Assert.Equal(22, CardNames(await PageAsync(token, "?inDate=true&inDate=false")).Count);
    }

    [Fact]
    public async Task The_chips_are_Tat_ca_and_the_five_categories_and_one_chip_filters_the_items()
    {
        var token = await SavedHamperAsync(Delivery);

        var all = await PageAsync(token);
        var chips = Regex.Matches(all, @"<button type=""submit"" class=""btn btn--outline btn--chip"" name=""category"" value=""([^""]*)"" aria-pressed=""(true|false)"">([^<]*)</button>")
            .Select(m => (Id: m.Groups[1].Value, Pressed: m.Groups[2].Value, Label: m.Groups[3].Value)).ToList();
        Assert.Equal(["Tất cả", "Bánh", "Hạt và trái cây sấy", "Mật ong và đặc sản", "Mứt và kẹo", "Trà và cà phê"], chips.Select(c => c.Label));
        Assert.Equal("true", chips[0].Pressed);

        // Bánh in date for 15/01/2027: Bánh quy bơ (10/01) is out; the other four are in.
        var banh = await PageAsync(token, "?category=banh");
        Assert.Equal(new[] { "Bánh đậu xanh", "Bánh gừng", "Bánh pía mini", "Bánh quy yến mạch" }.Order(StringComparer.Ordinal), CardNames(banh).Order(StringComparer.Ordinal));
        Assert.Contains("Hiện 4 món. 1 món hết hạn trước ngày giao đang được ẩn.", banh);
        Assert.Contains("name=\"category\" value=\"banh\" aria-pressed=\"true\"", banh);
        Assert.Matches(@"<a href=""/builder\?category=banh&inDate=false#chon-mon"">Hiện cả 1 món</a>", banh);   // decoded: &amp; became &

        // An unknown category (a typo, an old link) shows the items of every category.
        Assert.Equal(22, CardNames(await PageAsync(token, "?category=khong-co")).Count);
    }

    [Fact]
    public async Task A_sold_out_item_shows_Het_hang_instead_of_its_expiry()
    {
        var token = await SavedHamperAsync(Delivery);
        var stockBefore = await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "tra-sen").Select(p => p.Stock).SingleAsync());
        await SetStockAsync("tra-sen", 0);
        try
        {
            var html = await PageAsync(token);
            var card = Regex.Match(html, @"<li class=""card item-card item-card--sold-out"" id=""mon-tra-sen"">[\s\S]*?</li>").Value;

            Assert.Contains("Trà sen", card);
            Assert.Contains("<span class=\"badge badge--muted\">Hết hàng</span>", card);
            Assert.Contains("<button type=\"button\" class=\"btn btn--primary qty-add\" disabled>Hết hàng</button>", card);
            Assert.DoesNotContain("HSD", card);
            Assert.Single(Regex.Matches(html, "item-card--sold-out"));
        }
        finally
        {
            await SetStockAsync("tra-sen", stockBefore);
        }
    }

    [Fact]
    public async Task A_wrong_budget_keeps_the_items_of_the_date_saved_before()
    {
        var token = await SavedHamperAsync(Delivery);
        var client = Client();

        var get = await client.SendAsync(WithCookie("/builder", token));
        var page = WebUtility.HtmlDecode(await get.Content.ReadAsStringAsync());
        var antiForgeryCookie = get.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Antiforgery")).Split(';')[0];
        var post = new HttpRequestMessage(HttpMethod.Post, "/builder")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["budget"] = "abc",
                ["__RequestVerificationToken"] = Regex.Match(page, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value,
            }),
        };
        post.Headers.Add("Cookie", $"{HamperService.CookieName}={token}; {antiForgeryCookie}");

        var html = WebUtility.HtmlDecode(await (await client.SendAsync(post)).Content.ReadAsStringAsync());

        Assert.Contains("Nhập ngân sách bằng số", html);
        Assert.Equal(22, CardNames(html).Count);
    }

    // ---------- Search and sort (F17, F19) ----------

    private static List<string> Sorted(IEnumerable<string> names) => names.Order(StringComparer.Ordinal).ToList();

    [Fact]
    public async Task Search_ignores_accents_and_case_and_looks_in_name_brand_and_origin()
    {
        var token = await SavedHamperAsync(Delivery);

        Assert.Equal(Sorted(["Mứt dâu", "Mứt gừng", "Mứt hạt sen"]), Sorted(CardNames(await PageAsync(token, "?q=mut"))));
        Assert.Equal(Sorted(["Mứt dâu", "Mứt gừng", "Mứt hạt sen"]), Sorted(CardNames(await PageAsync(token, "?q=M%E1%BB%A8T"))));   // "MỨT"
        Assert.Equal(Sorted(["Cà phê rang xay", "Hạt macca", "Mật ong hoa cà phê"]), Sorted(CardNames(await PageAsync(token, "?q=dak+lak"))));   // origin Đắk Lắk: đ is folded too
        Assert.Equal(Sorted(["Bánh pía mini", "Mật ong hoa cà phê", "Mật ong hoa nhãn", "Tiêu đen Phú Quốc"]), Sorted(CardNames(await PageAsync(token, "?q=thuong+hieu+g"))));   // brand
        Assert.Contains("value=\"mut\"", await PageAsync(token, "?q=++mut++"));   // the box shows the words, trimmed
    }

    [Fact]
    public async Task Percent_and_underscore_are_plain_characters_and_no_match_shows_the_empty_box()
    {
        var token = await SavedHamperAsync(Delivery);

        foreach (var query in new[] { "?q=%25", "?q=_" })
        {
            var html = await PageAsync(token, query);
            Assert.Empty(CardNames(html));
            Assert.Contains("class=\"card no-match\"", html);
            Assert.Contains("Xóa bộ lọc", html);
        }

        Assert.Contains("Không có món nào khớp \"%\"", await PageAsync(token, "?q=%25"));
        Assert.Contains($"value=\"{new string('a', ItemCatalog.SearchMaxLength)}\"", await PageAsync(token, "?q=" + new string('a', 150)));
    }

    [Theory]
    [InlineData("price-asc", "Muối tôm Tây Ninh", "Kẹo dừa")]          // 50.000đ, 55.000đ (Kẹo lạc, 45.000đ, is out of date)
    [InlineData("price-desc", "Trà shan tuyết", "Hạt macca")]          // 220.000đ, 190.000đ
    [InlineData("expiry", "Mật ong hoa nhãn", "Mật ong hoa cà phê")]   // 31/12/2028, 31/10/2028
    [InlineData("name", "Bánh đậu xanh", "Bánh gừng")]
    public async Task The_sort_menu_orders_the_items_and_shows_its_choice(string sort, string first, string second)
    {
        var html = await PageAsync(await SavedHamperAsync(Delivery), "?sort=" + sort);

        Assert.Equal([first, second], CardNames(html).Take(2));
        Assert.Matches($@"<option value=""{sort}"" selected=""selected"">", html);
    }

    [Fact]
    public async Task An_unknown_sort_is_the_default_order()
    {
        var token = await SavedHamperAsync(Delivery);

        Assert.Equal(CardNames(await PageAsync(token)), CardNames(await PageAsync(token, "?sort=zzz")));
    }

    [Fact]
    public async Task The_filters_combine_and_a_send_without_a_chip_keeps_the_category()
    {
        var token = await SavedHamperAsync(Delivery);

        // Search inside a category.
        Assert.Empty(CardNames(await PageAsync(token, "?q=mut&category=banh")));
        Assert.Equal(5, CardNames(await PageAsync(token, "?q=banh&category=banh&inDate=false")).Count);

        // Sort or checkbox: the form sends only the hidden category. A pressed chip comes before it, so the chip wins.
        Assert.Equal(5, CardNames(await PageAsync(token, "?q=&sort=&inDate=false&category=banh")).Count);
        Assert.Equal(Sorted(["Kẹo dừa", "Kẹo lạc", "Mứt dâu", "Mứt gừng", "Mứt hạt sen"]), Sorted(CardNames(await PageAsync(token, "?inDate=false&category=mut-keo&category=banh"))));

        // ...which only works if the page really puts the hidden category after the chips.
        var html = await PageAsync(token, "?category=banh");
        var lastChip = html.LastIndexOf("name=\"category\" value=\"tra-ca-phe\"", StringComparison.Ordinal);
        var hidden = html.IndexOf("<input type=\"hidden\" name=\"category\" value=\"banh\">", StringComparison.Ordinal);
        Assert.True(lastChip > 0 && hidden > lastChip, "the hidden category must come after the chips");
    }

    [Fact]
    public async Task Show_all_keeps_the_search_and_sort_and_Enter_presses_a_hidden_search_button()
    {
        var html = await PageAsync(await SavedHamperAsync(Delivery), "?q=banh&sort=price-asc");

        Assert.Contains("Hiện 4 món. 1 món hết hạn trước ngày giao đang được ẩn.", html);
        Assert.Matches(@"<a href=""/builder\?q=banh&sort=price-asc&inDate=false#chon-mon"">Hiện cả 1 món</a>", html);

        var form = Regex.Match(html, @"<form class=""item-filters""[^>]*>([\s\S]*?)</form>").Groups[1].Value;
        var firstButton = Regex.Match(form, @"<button[^>]*>[^<]*</button>").Value;
        Assert.Equal("<button type=\"submit\" class=\"visually-hidden\" tabindex=\"-1\" aria-hidden=\"true\">Tìm</button>", firstButton);
    }

    // ---------- A saved date that has passed ----------

    private static DateOnly TodayInVietnam => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    [Theory]
    [InlineData(-30)]
    [InlineData(-1)]
    [InlineData(0)]    // today cannot be delivered either: the earliest is tomorrow
    public async Task A_saved_date_that_has_passed_locks_the_items_and_warns_under_the_date(int daysFromToday)
    {
        var date = TodayInVietnam.AddDays(daysFromToday);
        var html = await PageAsync(await SavedHamperAsync(date));

        Assert.Contains("class=\"lock-note\"", html);
        Assert.Empty(CardNames(html));
        Assert.Equal("1", CurrentStep(html));
        Assert.Contains($"value=\"{date:yyyy-MM-dd}\" aria-invalid=\"true\" aria-describedby=\"delivery-date-error\"", html);
        Assert.Contains("<p class=\"field__error\" id=\"delivery-date-error\">Ngày giao hàng sớm nhất là ngày mai.</p>", html);
        Assert.Contains("value=\"500.000đ\"", html);   // the budget stays
    }

    [Fact]
    public async Task A_saved_date_of_tomorrow_still_unlocks_the_items()
    {
        var html = await PageAsync(await SavedHamperAsync(TodayInVietnam.AddDays(1)));

        Assert.DoesNotContain("để mở bước chọn món và đóng gói", html);   // the items' lock note (packaging has its own)
        Assert.DoesNotContain("field__error", html);
        Assert.NotEmpty(CardNames(html));
    }

    // ---------- Odd input from the address ----------

    [Fact]
    public async Task Odd_filter_values_fall_back_to_the_defaults_instead_of_failing()
    {
        var token = await SavedHamperAsync(Delivery);

        Assert.Equal(22, CardNames(await PageAsync(token, "?inDate=abc")).Count);          // not a yes/no: the box stays on
        Assert.Equal(22, CardNames(await PageAsync(token, "?category=BANH")).Count);       // ids are lower case: unknown, so all
        Assert.Matches(@"<option value="""" selected=""selected"">Mặc định</option>", await PageAsync(token, "?sort=PRICE-ASC"));
    }

    [Fact]
    public async Task Search_still_matches_with_decomposed_letters_extra_spaces_and_line_breaks()
    {
        var token = await SavedHamperAsync(Delivery);
        var decomposed = Uri.EscapeDataString("Mứt".Normalize(System.Text.NormalizationForm.FormD));   // what some phone keyboards send

        Assert.Equal(3, CardNames(await PageAsync(token, "?q=" + decomposed)).Count);
        var spaced = await PageAsync(token, "?q=++banh+++quy%0A");
        Assert.Equal(["Bánh quy yến mạch"], CardNames(spaced));                              // Bánh quy bơ is out of date
        Assert.Contains("value=\"banh quy\"", spaced);
    }

    [Fact]
    public async Task A_backslash_or_markup_in_the_search_is_plain_text()
    {
        var token = await SavedHamperAsync(Delivery);

        Assert.Empty(CardNames(await PageAsync(token, "?q=%5C")));                          // \ is the escape character of the search

        var response = await Client().SendAsync(WithCookie("/builder?q=%3Cb%3Ex%3C%2Fb%3E", token));
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<b>x</b>", raw);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", raw);
    }

    [Fact]
    public async Task A_HEAD_request_is_answered_like_the_home_page()
    {
        var response = await Client().SendAsync(new HttpRequestMessage(HttpMethod.Head, "/builder"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- The page is well formed in every state ----------

    [Fact]
    public async Task The_page_is_well_formed_locked_unlocked_unticked_with_no_match_and_after_a_wrong_budget()
    {
        var token = await SavedHamperAsync(Delivery);
        var client = Client();

        AssertWellFormed("locked", await RawAsync(client, WithCookie("/builder", "none")));
        AssertWellFormed("unlocked", await RawAsync(client, WithCookie("/builder", token)));
        AssertWellFormed("unticked", await RawAsync(client, WithCookie("/builder?inDate=false&category=banh", token)));
        AssertWellFormed("no match", await RawAsync(client, WithCookie("/builder?q=zzz", token)));
        AssertWellFormed("date has passed", await RawAsync(client, WithCookie("/builder", await SavedHamperAsync(TodayInVietnam.AddDays(-1)))));

        var get = await client.SendAsync(WithCookie("/builder", token));
        var page = WebUtility.HtmlDecode(await get.Content.ReadAsStringAsync());
        var post = new HttpRequestMessage(HttpMethod.Post, "/builder")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["budget"] = "abc",
                ["deliveryDate"] = "x",
                ["__RequestVerificationToken"] = Regex.Match(page, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value,
            }),
        };
        post.Headers.Add("Cookie", $"{HamperService.CookieName}={token}; {get.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Antiforgery")).Split(';')[0]}");
        AssertWellFormed("wrong budget and date", await RawAsync(client, post));
    }

    private static async Task<string> RawAsync(HttpClient client, HttpRequestMessage request)
    {
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    // Mistakes a browser forgives silently but a screen reader or a click does not: a repeated id, a label or an aria
    // reference that points at nothing, a field without a label, a CSS, image or script file that does not exist, a
    // form that sends to an address the app does not have.
    internal static void AssertWellFormed(string state, string html)
    {
        foreach (Match form in Regex.Matches(html, @"<form\b[^>]*>"))
        {
            var method = Regex.Match(form.Value, @"\smethod=""([^""]+)""").Groups[1].Value.ToUpperInvariant() is { Length: > 0 } m ? m : "GET";
            var action = Regex.Match(form.Value, @"\saction=""([^""?#]*)").Groups[1].Value;
            Assert.True(RoutesTests.Answers(action, method), $"{state}: a form sends {method} to {action}, which the app does not answer: {form.Value}");
        }

        var ids = Regex.Matches(html, @"\sid=""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
        var repeated = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(repeated.Count == 0, $"{state}: repeated id {string.Join(", ", repeated)}");

        var labelled = Regex.Matches(html, @"<label[^>]*\sfor=""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
        foreach (var target in labelled.Concat(Regex.Matches(html, @"aria-(?:labelledby|describedby|controls)=""([^""]+)""").Select(m => m.Groups[1].Value)))
        {
            Assert.True(ids.Contains(target), $"{state}: a label or aria reference points at #{target}, which is not on the page");
        }

        foreach (Match control in Regex.Matches(html, @"<(input|select|textarea)\b[^>]*>"))
        {
            var tag = control.Value;
            if (Regex.IsMatch(tag, @"type=""(hidden|submit)""") || tag.Contains("aria-label=\""))
            {
                continue;
            }

            var id = Regex.Match(tag, @"\sid=""([^""]+)""").Groups[1].Value;
            Assert.True(id.Length > 0 && labelled.Contains(id), $"{state}: a field has no label: {tag}");
        }

        foreach (Match link in Regex.Matches(html, @"(?:src|href)=""(/(?:css|images|js)/[^""?#]+)"))
        {
            var path = Unfingerprinted(link.Groups[1].Value);
            var file = path.StartsWith("/js/")
                ? Path.Combine(RepoRoot.WebProject, "Scripts", Path.ChangeExtension(Path.GetFileName(path), ".ts"))   // built by npm, not committed
                : Path.Combine(RepoRoot.WebProject, "wwwroot", path.TrimStart('/'));
            Assert.True(File.Exists(file), $"{state}: {path} does not exist ({file})");
        }
    }

    // "/css/tokens.rn3hf4q12m.css" -> "/css/tokens.css": the framework writes a "~/" address with the file's
    // fingerprint (MapStaticAssets), so a changed file gets a new address.
    internal static string Unfingerprinted(string path) => Regex.Replace(path, @"\.[a-z0-9]{10}(\.[a-z0-9]+)$", "$1");

    private static HttpRequestMessage WithCookie(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"{HamperService.CookieName}={token}");
        return request;
    }

    private Task SetStockAsync(string slug, int stock) =>
        fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == slug).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, stock)));
}
