using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Data;
using Peekbox.Web.Security.Admin;
using Peekbox.Web.Services;

using static Peekbox.Tests.AdminOrdersKit;

namespace Peekbox.Tests;

// The orders of the admin (F57; mockups 02 and state 14): who may open them, the day and its count, the filters and the search,
// what a row and the open order show, and moving an order one step forward with its tracking code and note. Every test uses a
// delivery day of its own (AdminOrdersKit.Day).
[Collection(DatabaseCollection.Name)]
public class AdminOrdersTests(PeekboxFixture fixture)
{
    private async Task<Visitor> OwnerAsync(string role = AdminRoles.Owner) =>
        await AdminTestKit.SignedInAsync(fixture, await AdminTestKit.CreateUserAsync(fixture, role));

    private static string Ymd(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static string Table(string html) => Regex.Match(html, @"<table class=""admin-table""[\s\S]*?</table>").Value;

    private static string Detail(string html) => Regex.Match(html, @"<aside class=""card admin-detail""[\s\S]*?</aside>").Value;

    private async Task<string> ListAsync(Visitor visitor, string query) => await visitor.PageAsync("/admin/orders" + query);

    // ---- who ----

    [Fact]
    public async Task The_orders_screen_asks_for_a_sign_in_and_the_menu_leads_to_it()
    {
        var anonymous = await fixture.Client().GetAsync("/admin/orders");
        Assert.StartsWith("/admin/login", AdminTestKit.Where(anonymous));

        var owner = await OwnerAsync();
        Assert.Equal("/admin/orders", (await owner.GetAsync("/admin")).Headers.Location?.OriginalString);
        var page = await owner.GetAsync("/admin/orders");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("no-store", page.Headers.CacheControl?.ToString());
        Assert.Matches(@"<a class=""admin-side__link"" href=""/admin/orders"" aria-current=""page"">", html);
        Assert.Contains("aria-current=\"page\">Đơn hàng</a>", System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains("<title>Đơn hàng · Quản trị · Peekbox</title>", System.Net.WebUtility.HtmlDecode(html));
    }

    [Theory]
    [InlineData(AdminRoles.Owner)]
    [InlineData(AdminRoles.Manager)]
    [InlineData(AdminRoles.Staff)]
    public async Task Every_role_may_open_the_orders_and_move_one(string role)
    {
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day);
        var visitor = await OwnerAsync(role);
        await ListAsync(visitor, $"?date={Ymd(day)}");

        var response = await visitor.PostAsync($"/admin/orders/{id}/save", new() { ["from"] = "new", ["to"] = "preparing" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(OrderStatus.Preparing, (await ReloadAsync(fixture, id)).Status);
    }

    // ---- the day, the count and the empty state ----

    [Fact]
    public async Task With_no_date_in_the_address_the_screen_shows_today_and_a_date_in_the_address_shows_that_day()
    {
        var today = TestDates.TodayInVietnam;
        var day = AdminOrdersKit.Day();
        var todays = await OrderAsync(fixture, today, recipient: "Hôm Nay " + Guid.NewGuid().ToString("N")[..6]);
        var other = await OrderAsync(fixture, day, recipient: "Ngày Khác " + Guid.NewGuid().ToString("N")[..6]);
        var visitor = await OwnerAsync();

        var plain = await visitor.PageAsync("/admin/orders");
        var chosen = await ListAsync(visitor, $"?date={Ymd(day)}");

        Assert.Contains(PeekNumber(todays), Table(plain));
        Assert.DoesNotContain(PeekNumber(other), Table(plain));
        Assert.Contains(PeekNumber(other), Table(chosen));
        Assert.DoesNotContain(PeekNumber(todays), Table(chosen));
        Assert.Contains($"value=\"{Ymd(today)}\"", plain);   // the day picker says which day
        Assert.Matches($@"Giao ngày {today:dd/MM/yyyy}: <strong>\d+ đơn</strong>", plain);
        Assert.Contains($"Giao ngày {day:dd/MM/yyyy}: <strong>1 đơn</strong>", chosen);
    }

    private static string PeekNumber(int id) => $"PEEK{id:D6}";

    [Fact]
    public async Task An_empty_date_shows_every_day_and_text_that_is_not_a_date_is_today()
    {
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day);
        var visitor = await OwnerAsync();

        var every = await ListAsync(visitor, "?date=");
        var garbage = await ListAsync(visitor, "?date=not-a-date");

        Assert.Contains(PeekNumber(id), Table(every));
        Assert.Contains("Mọi ngày giao:", every);
        Assert.Contains($"value=\"{Ymd(TestDates.TodayInVietnam)}\"", garbage);
    }

    [Fact]
    public async Task A_day_with_no_orders_says_so_and_offers_today_unless_it_is_today()
    {
        var empty = AdminOrdersKit.Day();
        var visitor = await OwnerAsync();

        var html = await ListAsync(visitor, $"?date={Ymd(empty)}");

        Assert.Contains($"Chưa có đơn giao ngày {empty:dd/MM/yyyy}", html);
        Assert.Contains("Đơn mới sẽ hiện ở đây. Đổi ngày giao hoặc xóa bộ lọc để xem ngày khác.", html);
        Assert.Contains($"<a class=\"btn btn--outline\" href=\"/admin/orders?date={Ymd(TestDates.TodayInVietnam)}\">Xem hôm nay</a>", html);
        Assert.Contains("Giao ngày " + empty.ToString("dd/MM/yyyy") + ": <strong>0 đơn</strong>", html);
        Assert.DoesNotContain("<table", html);

        // Today itself: the same message, no button to go where we are. (Other tests may have left orders on today: only if empty.)
        var today = await ListAsync(visitor, $"?date={Ymd(TestDates.TodayInVietnam)}&q=zzz-nothing-{Guid.NewGuid():N}");
        Assert.Contains("Không có đơn nào khớp", today);
    }

    [Fact]
    public async Task The_count_of_the_day_ignores_the_status_filter_and_the_search()
    {
        var day = AdminOrdersKit.Day();
        await OrderAsync(fixture, day, OrderStatus.New);
        await OrderAsync(fixture, day, OrderStatus.Preparing);
        await OrderAsync(fixture, day, OrderStatus.Cancelled);
        var visitor = await OwnerAsync();

        var filtered = await ListAsync(visitor, $"?date={Ymd(day)}&status=new&q=Trần");

        Assert.Contains("<strong>3 đơn</strong>", filtered);
        Assert.Single(Regex.Matches(Table(filtered), @"<tr class=""admin-row"));
    }

    // ---- filters and search ----

    [Fact]
    public async Task The_status_filter_shows_only_that_status_and_an_unknown_status_means_all()
    {
        var day = AdminOrdersKit.Day();
        var fresh = await OrderAsync(fixture, day, OrderStatus.New);
        var going = await OrderAsync(fixture, day, OrderStatus.Delivering);
        var visitor = await OwnerAsync();

        var only = await ListAsync(visitor, $"?date={Ymd(day)}&status=delivering");
        var unknown = await ListAsync(visitor, $"?date={Ymd(day)}&status=banana");

        Assert.Contains(PeekNumber(going), Table(only));
        Assert.DoesNotContain(PeekNumber(fresh), Table(only));
        Assert.Matches(@"<option value=""delivering"" selected=""selected"">", only);
        Assert.Contains(PeekNumber(fresh), Table(unknown));
        Assert.Contains(PeekNumber(going), Table(unknown));
    }

    [Theory]
    [InlineData("number")]        // PEEK000123
    [InlineData("digits")]        // 123
    [InlineData("phone")]         // 0900 111 111
    [InlineData("phone-part")]    // 111 111
    [InlineData("recipient")]     // tran thi (no accents, lower case)
    [InlineData("sender")]        // NGUYỄN văn a
    public async Task The_search_finds_an_order_by_number_phone_or_name_ignoring_accents_and_case(string how)
    {
        var day = AdminOrdersKit.Day();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var phone = "09" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10_000_000, 99_999_999);   // eight digits after 09
        var id = await OrderAsync(fixture, day, recipient: $"Trần Thị {unique}", sender: $"Nguyễn Văn {unique}", recipientPhone: phone);
        var other = await OrderAsync(fixture, day, recipient: "Lê Hoàng", sender: "Phạm Thu", recipientPhone: "0988000000", senderPhone: "0977000000");
        var visitor = await OwnerAsync();
        var term = how switch
        {
            "number" => PeekNumber(id),
            "digits" => id.ToString(),
            "phone" => $"{phone[..4]} {phone[4..7]} {phone[7..]}",
            "phone-part" => phone[3..9],
            "recipient" => $"tran thi {unique}".ToUpperInvariant().Replace("TRAN", "tran"),
            _ => $"NGUYEN VAN {unique}",
        };

        var html = await ListAsync(visitor, $"?date={Ymd(day)}&q={Uri.EscapeDataString(term)}");

        Assert.Contains(PeekNumber(id), Table(html));
        Assert.DoesNotContain(PeekNumber(other), Table(html));
    }

    [Fact]
    public async Task A_percent_and_an_underscore_in_the_search_are_plain_characters_and_nothing_found_says_so()
    {
        var day = AdminOrdersKit.Day();
        await OrderAsync(fixture, day, recipient: "Một Người");
        var visitor = await OwnerAsync();

        var wild = await ListAsync(visitor, $"?date={Ymd(day)}&q=%25");
        var under = await ListAsync(visitor, $"?date={Ymd(day)}&q=_");

        foreach (var html in new[] { wild, under })
        {
            Assert.Contains("Không có đơn nào khớp", html);
            Assert.Contains($"<a class=\"btn btn--outline\" href=\"/admin/orders?date={Ymd(day)}\">Xóa bộ lọc</a>", html);
        }
    }

    [Fact]
    public async Task The_filters_stay_in_the_form_and_in_the_links_to_an_order()
    {
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day, recipient: "Giữ Bộ Lọc");
        var visitor = await OwnerAsync();

        var html = await ListAsync(visitor, $"?date={Ymd(day)}&status=new&q=Gi%E1%BB%AF");

        Assert.Contains($"value=\"Giữ\"", System.Net.WebUtility.HtmlDecode(html));
        var links = string.Join(" ", Regex.Matches(html, "href=\"(/admin/orders[^\"]*)\"").Select(m => m.Groups[1].Value));
        Assert.Contains($"/admin/orders?date={Ymd(day)}&status=new&q=Gi%E1%BB%AF&order={id}", links);   // a name is escaped, so & inside it cannot end a part
        Assert.Contains("<noscript><button type=\"submit\" class=\"btn btn--outline admin-filters__apply\">Lọc</button></noscript>", html);
        Assert.Matches("<script type=\"module\" src=\"/js/admin-orders[^\"]*\\.js\"", html);   // the file name carries its fingerprint
    }

    // ---- the day chips (issue #105) ----

    private static string Chips(string html) => System.Net.WebUtility.HtmlDecode(Regex.Match(html, @"<div class=""admin-days""[\s\S]*?</div>").Value);

    [Theory]
    [InlineData("today", "?date={today}", true, false)]
    [InlineData("no date at all means today", "", true, false)]
    [InlineData("every day", "?date=", false, true)]
    [InlineData("another day", "?date={other}", false, false)]
    public async Task The_day_chips_mark_today_or_every_day_and_neither_for_another_day(string how, string query, bool todayIsCurrent, bool everyDayIsCurrent)
    {
        var today = TestDates.TodayInVietnam;
        var visitor = await OwnerAsync();

        var chips = Chips(await ListAsync(visitor, query.Replace("{today}", Ymd(today)).Replace("{other}", Ymd(today.AddDays(3)))));

        Assert.True(chips.Length > 0, how);
        Assert.Contains($"href=\"/admin/orders?date={Ymd(today)}\"{(todayIsCurrent ? " aria-current=\"true\"" : "")}>Hôm nay</a>", chips);
        Assert.Contains($"href=\"/admin/orders?date=\"{(everyDayIsCurrent ? " aria-current=\"true\"" : "")}>Mọi ngày</a>", chips);
    }

    [Fact]
    public async Task The_day_chips_keep_the_status_and_the_search_and_leave_the_open_order_out()
    {
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day, recipient: "Giữ Bộ Lọc");
        var visitor = await OwnerAsync();

        var chips = Chips(await ListAsync(visitor, $"?date={Ymd(day)}&status=new&q=Gi%E1%BB%AF&order={id}"));

        Assert.Contains($"href=\"/admin/orders?date={Ymd(TestDates.TodayInVietnam)}&status=new&q=Gi%E1%BB%AF\"", chips);
        Assert.Contains("href=\"/admin/orders?date=&status=new&q=Gi%E1%BB%AF\"", chips);
        Assert.DoesNotContain("order=", chips);
    }

    // ---- what a row and the open order show ----

    [Fact]
    public async Task A_row_shows_the_number_recipient_items_total_payment_and_status()
    {
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day, OrderStatus.Delivering, PaymentMethod.CashOnDelivery, PaymentStatus.Pending, recipient: "Hàng Một", quantity: 3);
        var visitor = await OwnerAsync();

        var row = Table(await ListAsync(visitor, $"?date={Ymd(day)}"));

        Assert.Contains($">{PeekNumber(id)}</a>", row);
        Assert.Contains("Hàng Một", System.Net.WebUtility.HtmlDecode(row));
        Assert.Contains("3<span class=\"admin-row__unit\"> món</span>", row);
        Assert.Contains("<td class=\"admin-row__name\">Hàng Một<span class=\"admin-row__count\"> · 3 món</span></td>", System.Net.WebUtility.HtmlDecode(row));   // the phone's "Name · 3 món", in the same text
        Assert.Contains("admin-status--delivering\">Đang giao</span>", row);
        Assert.Contains("<td class=\"admin-row__pay\">Khi nhận hàng</td>", row);
        Assert.Contains("đ</td>", row);   // the total, in whole dong
    }

    [Theory]
    [InlineData(PaymentMethod.ZaloPay, PaymentStatus.Paid, "ZaloPay, đã trả")]
    [InlineData(PaymentMethod.ZaloPay, PaymentStatus.Pending, "ZaloPay, chờ trả")]
    [InlineData(PaymentMethod.ZaloPay, PaymentStatus.Failed, "ZaloPay, thanh toán lỗi")]
    [InlineData(PaymentMethod.ZaloPay, PaymentStatus.Refunded, "ZaloPay, đã hoàn tiền")]
    [InlineData(PaymentMethod.CashOnDelivery, PaymentStatus.Pending, "Khi nhận hàng")]
    [InlineData(PaymentMethod.CashOnDelivery, PaymentStatus.Paid, "Khi nhận hàng, đã thu")]
    public void Each_way_of_paying_has_its_short_words(PaymentMethod method, PaymentStatus status, string words) =>
        Assert.Equal(words, OrderLabels.PaymentShort(method, status));

    [Fact]
    public async Task The_newest_order_of_the_list_is_open_by_default_and_an_order_in_the_address_is_open_even_when_the_filters_hide_it()
    {
        var day = AdminOrdersKit.Day();
        var older = await OrderAsync(fixture, day, recipient: "Cũ Hơn");
        var newer = await OrderAsync(fixture, day, recipient: "Mới Hơn");
        var visitor = await OwnerAsync();

        var plain = await ListAsync(visitor, $"?date={Ymd(day)}");
        var chosen = await ListAsync(visitor, $"?date={Ymd(day)}&order={older}");
        var hidden = await ListAsync(visitor, $"?date={Ymd(day)}&status=delivered&order={older}");

        Assert.Contains(PeekNumber(newer), Detail(plain));
        Assert.True(plain.IndexOf(PeekNumber(newer), StringComparison.Ordinal) < plain.IndexOf(PeekNumber(older), StringComparison.Ordinal));   // newest first
        Assert.Contains(PeekNumber(older), Detail(chosen));
        Assert.Contains("admin-row admin-row--open", chosen);
        Assert.Contains(PeekNumber(older), Detail(hidden));
        Assert.DoesNotContain("<table", hidden);
    }

    [Fact]
    public async Task An_order_that_does_not_exist_in_the_address_opens_nothing_and_fails_nothing()
    {
        var day = AdminOrdersKit.Day();
        await OrderAsync(fixture, day);
        var visitor = await OwnerAsync();

        foreach (var order in new[] { "999999999", "0", "-3", "abc", "99999999999999999999" })
        {
            var response = await visitor.GetAsync($"/admin/orders?date={Ymd(day)}&order={order}");
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"order={order} gave {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task The_open_order_shows_who_where_what_and_how_much()
    {
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day, recipient: "Trần Thị B", sender: "Nguyễn Văn A", cardMessage: "Chúc mừng\nnăm mới", hidePrices: true,
            couponCode: "TET10", discount: 36_000, quantity: 2, window: DeliveryWindow.Afternoon);
        var visitor = await OwnerAsync();

        var detail = WebDecode(Detail(await ListAsync(visitor, $"?date={Ymd(day)}&order={id}")));

        Assert.Contains(PeekNumber(id), detail);
        Assert.Contains($"Giao {day:dd/MM/yyyy}, chiều", detail);
        Assert.Contains("Người nhận: Trần Thị B, 0900 111 111", detail);
        Assert.Contains("Địa chỉ: 12 đường mẫu,", detail);
        Assert.Contains("Chuyển lần lượt: Mới, Đang chuẩn bị, Đang giao, Đã giao.", detail);   // built from the labels, in order, without the cancelled one
        Assert.Contains("Người gửi: Nguyễn Văn A, 0900 000 000, ten@example.com", detail);
        Assert.Contains("Lời nhắn trên thiệp:", detail);
        Assert.Contains("Chúc mừng\nnăm mới", System.Net.WebUtility.HtmlDecode(Regex.Match(Detail(await ListAsync(visitor, $"?date={Ymd(day)}&order={id}")), @"admin-detail__card"">([\s\S]*?)</span>").Groups[1].Value));
        Assert.Contains("Gói quà: Ẩn giá bên trong hộp quà", detail);
        Assert.Contains("Trà sen × 2", detail);
        Assert.DoesNotContain("Thương hiệu Thương hiệu", detail);   // the brand is printed as it is saved, with no word added before it
        Assert.Contains("HSD", detail);
        Assert.Contains("Hộp cứng có nơ 40.000đ", detail);
        Assert.Contains("Mã giảm giá TET10 −36.000đ", detail);
        Assert.Contains("Tổng (gồm giao hàng 25.000đ)", detail);
    }

    private static string WebDecode(string html) => System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, @"<[^>]+>", " "), @"\s+", " "));

    [Fact]
    public async Task Text_typed_into_a_name_or_a_note_is_shown_as_text_not_as_a_page()
    {
        var day = AdminOrdersKit.Day();
        var id = await OrderAsync(fixture, day, recipient: "<img src=x onerror=alert(1)>", adminNote: "<script>alert(2)</script>");
        var visitor = await OwnerAsync();

        var raw = await (await visitor.GetAsync($"/admin/orders?date={Ymd(day)}&order={id}")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("<img src=x", raw);
        Assert.DoesNotContain("<script>alert", raw);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", raw);
        Assert.Contains("&lt;script&gt;alert(2)&lt;/script&gt;", raw);
    }

    [Fact]
    public async Task A_day_over_the_list_limit_shows_the_newest_and_says_the_rest_are_cut()
    {
        var day = AdminOrdersKit.Day();
        await fixture.WithDbAsync(async db =>
        {
            var ward = await db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == 1);
            var product = await db.Products.AsNoTracking().SingleAsync(p => p.Slug == "tra-sen");
            for (var i = 0; i < AdminOrders.ListLimit + 5; i++)
            {
                db.Orders.Add(new Order
                {
                    Token = Peekbox.Web.Security.SecretToken.New(), Status = OrderStatus.New, PaymentMethod = PaymentMethod.CashOnDelivery, PaymentStatus = PaymentStatus.Pending,
                    SenderName = "A", SenderPhone = "0900000000", SenderEmail = "a@example.com", RecipientName = "B", RecipientPhone = "0900111111", WardId = ward.Id, WardName = ward.Name,
                    Street = "1", DeliveryDate = day, DeliveryWindow = DeliveryWindow.Morning, GoodsVnd = 1000, PackagingName = "x", PackagingFeeVnd = 0, DeliveryFeeVnd = 0, TotalVnd = 1000,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Items = [new OrderItem { ProductId = product.Id, Name = "x", Brand = "x", Origin = "x", WeightGrams = 1, UnitPriceVnd = 1000, Quantity = 1 }],
                });
            }

            return await db.SaveChangesAsync();
        });
        var visitor = await OwnerAsync();

        var html = await ListAsync(visitor, $"?date={Ymd(day)}");

        Assert.Equal(AdminOrders.ListLimit, Regex.Matches(Table(html), @"<tr class=""admin-row").Count);
        Assert.Contains($"<strong>{AdminOrders.ListLimit + 5} đơn</strong>", html);
        Assert.Contains($"Chỉ hiện {AdminOrders.ListLimit} đơn mới nhất.", html);
    }
}
