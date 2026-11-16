using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Models;
using Peekbox.Web.Services;

using static Peekbox.Tests.TestDates;

namespace Peekbox.Tests;

// The checkout page (F41 to F48) and the sold-out page (F50), through HTTP. Seed: Trà sen 180.000 (stock 120, expires
// 31/12/2027), Bánh pía mini 120.000 (30/04/2027); hard box with bow 40.000; the first ring-1 ward costs 25.000 to
// deliver to. Amounts are written out on purpose. Every test that places an order puts the stock back.
[Collection(DatabaseCollection.Name)]
public class CheckoutTests(PeekboxFixture fixture)
{
    private async Task<string> HamperAsync(DateOnly? delivery, params (string Slug, int Quantity)[] lines)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(async db =>
        {
            var hamper = new Hamper
            {
                Token = token, BudgetVnd = 500_000, DeliveryDate = delivery, PackagingId = "hard-bow",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            };
            foreach (var (slug, quantity) in lines)
            {
                var product = await db.Products.SingleAsync(p => p.Slug == slug);
                hamper.Items.Add(new HamperItem { ProductId = product.Id, Quantity = quantity });
            }

            db.Hampers.Add(hamper);
            return await db.SaveChangesAsync();
        });
        return token;
    }

    private async Task<Visitor> VisitorAsync(params (string Slug, int Quantity)[] lines) => new(fixture.Client(), await HamperAsync(Delivery, lines));

    private Task<Ward> WardAsync(int ring) =>
        fixture.WithDbAsync(db => db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == ring));

    private Task<Dictionary<string, int>> StockAsync() =>
        fixture.WithDbAsync(db => db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Stock));

    private Task SetStockAsync(string slug, int stock) =>
        fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == slug).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, stock)));

    private static Dictionary<string, string> Form(int wardId, string payment = "ZaloPay") => new()
    {
        ["senderName"] = "Nguyễn Văn A",
        ["senderPhone"] = "0900 000 000",
        ["senderEmail"] = "ten@example.com",
        ["recipientName"] = "Trần Thị B",
        ["recipientPhone"] = "0900.111.111",
        ["ward"] = wardId.ToString(CultureInfo.InvariantCulture),
        ["street"] = "12 đường mẫu",
        ["window"] = "Afternoon",
        ["payment"] = payment,
    };

    private static string Summary(string html) => Regex.Match(html, @"<aside class=""card checkout-summary""[\s\S]*?</aside>").Value;

    private static string Bar(string html) => Regex.Match(html, @"<div class=""checkout-bar""[\s\S]*?</div>").Value;

    [Fact]
    public async Task A_box_that_cannot_be_ordered_goes_back_to_the_cart()
    {
        var noHamper = new Visitor(fixture.Client(), null);
        var empty = new Visitor(fixture.Client(), await HamperAsync(Delivery));
        var noDate = new Visitor(fixture.Client(), await HamperAsync(null, ("tra-sen", 1)));
        var passed = new Visitor(fixture.Client(), await HamperAsync(TodayInVietnam, ("tra-sen", 1)));
        var outOfDate = new Visitor(fixture.Client(), await HamperAsync(new DateOnly(2027, 4, 28), ("tra-sen", 1), ("banh-pia", 1)));   // Bánh pía mini expires 30/04/2027
        var ward = await WardAsync(1);

        foreach (var visitor in new[] { noHamper, empty, noDate, passed, outOfDate })
        {
            Assert.Equal("/cart", (await visitor.GetAsync("/checkout")).Headers.Location?.OriginalString);
            Assert.Equal("/cart", (await visitor.PostAsync("/checkout", Form(ward.Id))).Headers.Location?.OriginalString);
        }
    }

    [Fact]
    public async Task The_page_shows_the_box_and_the_total_without_a_fee_until_a_ward_is_picked()
    {
        var visitor = await VisitorAsync(("tra-sen", 1));

        var html = await visitor.PageAsync("/checkout");

        Assert.Contains("<title>Thanh toán · Peekbox</title>", html);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html);
        Assert.DoesNotContain("checkout-alert", html);
        Assert.Contains("<form class=\"checkout-form\" id=\"checkout-form\" method=\"post\" action=\"/checkout\" novalidate>", html);
        Assert.Contains("<option value=\"\" selected=\"selected\">Chọn phường</option>", html);
        Assert.Equal(101 + 1, Regex.Matches(html, "<option ").Count);
        Assert.Contains($"<span>{VnDay(Delivery)}</span><a href=\"/builder#delivery-date\">Đổi ngày</a>", html);
        Assert.Contains("<input type=\"radio\" id=\"window-morning\" name=\"window\" value=\"Morning\" checked=\"checked\"><span>Sáng 8:00 – 12:00</span>", html);
        Assert.Contains("<input type=\"radio\" id=\"window-evening\" name=\"window\" value=\"Evening\"><span>Tối 17:00 – 21:00</span>", html);
        Assert.Contains("<input type=\"radio\" id=\"payment-zalopay\" name=\"payment\" value=\"ZaloPay\" checked=\"checked\">", html);
        Assert.Contains("<input type=\"radio\" id=\"payment-cod\" name=\"payment\" value=\"CashOnDelivery\">", html);

        var summary = Summary(html);
        Assert.Contains("<li><span>Trà sen <span class=\"checkout-lines__meta\">· 100 g</span></span><span>180.000đ</span></li>", summary);
        Assert.Contains("<div><dt>Tiền hàng</dt><dd>180.000đ</dd></div>", summary);
        Assert.Contains("<div><dt>Đóng gói (Hộp cứng có nơ)</dt><dd>40.000đ</dd></div>", summary);
        Assert.Contains("<div class=\"checkout-sums__later\"><dt>Phí giao hàng</dt><dd>Chọn phường để tính</dd></div>", summary);
        Assert.Contains("<span>Tạm tính (chưa gồm phí giao)</span> <strong>220.000đ</strong>", summary);
        Assert.Contains("<span>Tạm tính (chưa gồm phí giao)</span> <strong>220.000đ</strong>", Bar(html));
        Assert.Contains("Ngân sách 500.000đ chỉ tính tiền hàng, không tính phí giao hàng.", summary);
        Assert.Equal(2, Regex.Matches(html, "form=\"checkout-form\" class=\"btn btn--primary checkout-submit\"").Count);
    }

    [Fact]
    public async Task A_picked_ward_brings_its_fee_into_the_summary_and_the_total()
    {
        var visitor = await VisitorAsync(("tra-sen", 1));
        var near = await WardAsync(1);
        var far = await WardAsync(3);

        var html = await visitor.PageAsync($"/checkout?ward={near.Id}");

        Assert.Contains($"<option value=\"{near.Id}\" selected=\"selected\">{near.Name}</option>", html);
        Assert.Contains("<div><dt>Phí giao hàng</dt><dd>25.000đ</dd></div>", Summary(html));
        Assert.Contains("<span>Tổng cộng (đã gồm thuế)</span> <strong>245.000đ</strong>", Summary(html));
        Assert.Contains("<span>Tổng cộng (đã gồm thuế)</span> <strong>245.000đ</strong>", Bar(html));
        Assert.Contains("<strong>265.000đ</strong>", Summary(await visitor.PageAsync($"/checkout?ward={far.Id}")));
        Assert.Contains("Chọn phường để tính", Summary(await visitor.PageAsync("/checkout?ward=999999")));   // not a ward: no fee
    }

    [Fact]
    public async Task A_form_with_mistakes_comes_back_with_every_message_and_what_was_typed()
    {
        var stock = await StockAsync();
        var visitor = await VisitorAsync(("tra-sen", 1));
        await visitor.PageAsync("/checkout");
        var form = Form((await WardAsync(1)).Id);
        form["recipientPhone"] = "0900 111";
        form["street"] = "   ";
        form["senderEmail"] = "ten@example";

        var response = await visitor.PostAsync("/checkout", form);
        var html = await visitor.TextAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div class=\"alert checkout-alert\" role=\"alert\" tabindex=\"-1\" autofocus id=\"loi\"> <strong class=\"checkout-alert__title\">Cần sửa 3 chỗ trước khi đặt hàng</strong>", html);
        Assert.Contains("<li><a href=\"#sender-email\">Email chưa đúng dạng</a></li>", html);
        Assert.Contains("<li><a href=\"#recipient-phone\">Số điện thoại người nhận cần 10 chữ số</a></li>", html);
        Assert.Contains("<li><a href=\"#street\">Nhập số nhà và tên đường</a></li>", html);
        Assert.Contains("aria-invalid=\"true\" aria-describedby=\"recipient-phone-error\"> <p class=\"field__error\" id=\"recipient-phone-error\">Số điện thoại cần 10 chữ số. Bạn đã nhập 7 chữ số.</p>", html);
        Assert.Contains("<p class=\"field__error\" id=\"sender-email-error\">Email chưa đúng dạng. Ví dụ: ten@example.com</p>", html);
        Assert.Contains("value=\"ten@example\"", html);   // what was typed stays
        Assert.Contains("value=\"Nguyễn Văn A\"", html);
        Assert.Contains("<input type=\"radio\" id=\"window-afternoon\" name=\"window\" value=\"Afternoon\" checked=\"checked\">", html);
        Assert.Contains("<div><dt>Phí giao hàng</dt><dd>25.000đ</dd></div>", Summary(html));   // the ward picked is priced
        Assert.Single(Regex.Matches(html, "aria-invalid=\"true\" aria-describedby=\"sender-email-error\""));
        Assert.DoesNotContain("aria-invalid=\"true\" aria-describedby=\"sender-name-error\"", html);
        Assert.Equal(stock["tra-sen"], (await StockAsync())["tra-sen"]);   // nothing placed
    }

    [Fact]
    public async Task A_good_form_places_the_order_and_a_second_click_opens_the_same_order()
    {
        var stock = await StockAsync();
        try
        {
            var visitor = await VisitorAsync(("tra-sen", 2));
            await visitor.PageAsync("/checkout");
            var ward = await WardAsync(2);

            var first = await visitor.PostAsync("/checkout", Form(ward.Id, "CashOnDelivery"));
            var second = await visitor.PostAsync("/checkout", Form(ward.Id, "CashOnDelivery"));

            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
            var location = first.Headers.Location?.OriginalString;
            Assert.Matches("^/orders/[A-Za-z0-9_-]{43}$", location);
            Assert.Equal(location, second.Headers.Location?.OriginalString);
            var order = await fixture.WithDbAsync(db => db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => "/orders/" + o.Token == location));
            Assert.Equal(
                ("Nguyễn Văn A", "0900000000", "0900111111", ward.Name, 35_000L, 360_000L + 40_000L + 35_000L, DeliveryWindow.Afternoon, PaymentMethod.CashOnDelivery, PaymentStatus.Pending, Delivery),
                (order.SenderName, order.SenderPhone, order.RecipientPhone, order.WardName, order.DeliveryFeeVnd, order.TotalVnd, order.DeliveryWindow, order.PaymentMethod, order.PaymentStatus, order.DeliveryDate));
            Assert.Equal(("Trà sen", 2), (order.Items.Single().Name, order.Items.Single().Quantity));
            Assert.Equal(stock["tra-sen"] - 2, (await StockAsync())["tra-sen"]);

            // The hamper is an order now: the cart is empty and checkout sends a GET back to it.
            Assert.Contains("Giỏ hàng đang trống", await visitor.PageAsync("/cart"));
            Assert.Equal("/cart", (await visitor.GetAsync("/checkout")).Headers.Location?.OriginalString);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task An_item_that_ran_short_shows_once_what_is_left_and_nothing_is_placed()
    {
        var stock = await StockAsync();
        try
        {
            var visitor = await VisitorAsync(("tra-sen", 2), ("banh-pia", 1));
            await visitor.PageAsync("/checkout");
            var orders = await fixture.WithDbAsync(db => db.Orders.CountAsync());
            await SetStockAsync("tra-sen", 1);
            await SetStockAsync("banh-pia", 0);

            var response = await visitor.PostAsync("/checkout", Form((await WardAsync(1)).Id));

            Assert.Equal("/checkout/soldout", response.Headers.Location?.OriginalString);
            var html = await visitor.PageAsync("/checkout/soldout");
            Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html);
            Assert.Contains("<h2 class=\"checkout-stock__title\" id=\"stock-title\">Có món vừa hết hoặc không còn đủ</h2>", html);
            Assert.Contains("Bánh pía mini vừa có người khác đặt hết. Chúng tôi đã bỏ món này khỏi hộp của bạn.", html);
            Assert.Contains("Trà sen chỉ còn 1, hộp của bạn đã giảm từ 2 xuống 1.", html);
            Assert.Contains("Chưa có khoản nào bị tính.", html);
            Assert.Contains("<li class=\"checkout-stock__gone\"><span><span class=\"checkout-stock__name\">Bánh pía mini</span></span><span class=\"badge badge--muted\">Hết hàng</span></li>", html);
            Assert.Contains("<div><dt>Tiền hàng (1 món)</dt><dd>180.000đ</dd></div>", html);
            Assert.Contains("<a class=\"btn btn--outline\" href=\"/checkout\">Tiếp tục thanh toán với 1 món</a>", html);
            Assert.Equal((1, 0), ((await StockAsync())["tra-sen"], (await StockAsync())["banh-pia"]));   // nothing taken
            Assert.Equal(orders, await fixture.WithDbAsync(db => db.Orders.CountAsync()));   // the tests of this collection run one at a time

            // Shown once: a reload goes back to checkout, which now has the box that is left.
            Assert.Equal("/checkout", (await visitor.GetAsync("/checkout/soldout")).Headers.Location?.OriginalString);
            Assert.Contains("<div><dt>Tiền hàng</dt><dd>180.000đ</dd></div>", Summary(await visitor.PageAsync("/checkout")));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task Too_many_tries_from_one_address_get_the_page_with_a_note_and_a_429()
    {
        await using var strict = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:OrdersPer15Minutes", "1"));
        var visitor = new Visitor(fixture.Client(strict), await HamperAsync(Delivery, ("tra-sen", 1)));
        await visitor.PageAsync("/checkout");
        var form = Form((await WardAsync(1)).Id);
        form["street"] = "";   // the first try fails the checks, and still counts

        var first = await visitor.PostAsync("/checkout", form);
        var second = await visitor.PostAsync("/checkout", form);
        var html = await visitor.TextAsync(second);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Contains("<strong class=\"checkout-alert__title\">Bạn đã thử quá nhiều lần.</strong>", html);
        Assert.Contains("Hãy đợi vài phút rồi thử lại. Những gì bạn đã nhập vẫn được giữ.", html);
        Assert.Contains("value=\"Nguyễn Văn A\"", html);
        Assert.DoesNotContain("Cần sửa", html);
    }

    [Fact]
    public async Task A_post_without_the_form_token_is_refused()
    {
        var visitor = await VisitorAsync(("tra-sen", 1));
        await visitor.PageAsync("/checkout");

        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync("/checkout", Form((await WardAsync(1)).Id), withToken: false)).StatusCode);
    }

    [Fact]
    public async Task Every_state_of_the_checkout_is_well_formed()
    {
        var visitor = await VisitorAsync(("tra-sen", 1));
        var ward = await WardAsync(1);
        BuilderItemsTests.AssertWellFormed("first", await (await visitor.GetAsync("/checkout")).Content.ReadAsStringAsync());
        BuilderItemsTests.AssertWellFormed("ward", await (await visitor.GetAsync($"/checkout?ward={ward.Id}")).Content.ReadAsStringAsync());
        var form = Form(ward.Id);
        form["senderPhone"] = "";
        BuilderItemsTests.AssertWellFormed("errors", await (await visitor.PostAsync("/checkout", form)).Content.ReadAsStringAsync());
    }

    private static string VnDay(DateOnly date) => Peekbox.Web.Formatting.VnDate.Day(date);
}

// The field checks on their own (F46): what each typed value gives.
public class CheckoutCheckTests
{
    private static readonly IReadOnlySet<int> Wards = new HashSet<int> { 7 };

    private static CheckoutForm Good() => new()
    {
        SenderName = " Nguyễn Văn A ", SenderPhone = "0900 000 000", SenderEmail = "ten@example.com",
        RecipientName = "Trần Thị B", RecipientPhone = "0900-111-111", Ward = 7, Street = "12 đường mẫu",
        Window = DeliveryWindow.Evening, Payment = PaymentMethod.CashOnDelivery,
    };

    private static string? MessageFor(CheckoutForm form, string field) =>
        CheckoutCheck.Check(form, Wards, out _).SingleOrDefault(e => e.Field == field)?.Message;

    [Fact]
    public void A_good_form_gives_the_request_with_spaces_and_separators_dropped()
    {
        Assert.Empty(CheckoutCheck.Check(Good(), Wards, out var request));
        Assert.Equal(
            new OrderRequest("Nguyễn Văn A", "0900000000", "ten@example.com", "Trần Thị B", "0900111111", 7, "12 đường mẫu", DeliveryWindow.Evening, PaymentMethod.CashOnDelivery),
            request);
    }

    [Theory]
    [InlineData("", "Nhập số điện thoại người gửi.")]
    [InlineData("  ", "Nhập số điện thoại người gửi.")]
    [InlineData("090011111", "Số điện thoại cần 10 chữ số. Bạn đã nhập 9 chữ số.")]
    [InlineData("09001111111", "Số điện thoại cần 10 chữ số. Bạn đã nhập 11 chữ số.")]
    [InlineData("1900111111", "Số điện thoại bắt đầu bằng số 0.")]
    [InlineData("09001111a1", "Số điện thoại chỉ gồm chữ số.")]
    [InlineData("+84900111111", "Số điện thoại chỉ gồm chữ số.")]
    [InlineData("090011111٣", "Số điện thoại chỉ gồm chữ số.")]   // an Arabic-Indic three: a digit, but not 0 to 9
    public void A_phone_number_is_10_digits_starting_with_0(string typed, string message)
    {
        var form = Good();
        form.SenderPhone = typed;

        Assert.Equal(message, MessageFor(form, "sender-phone"));
        Assert.DoesNotContain(CheckoutCheck.Check(form, Wards, out var request), e => e.Field != "sender-phone");
        Assert.Null(request);
    }

    [Theory]
    [InlineData("", "Nhập email để nhận xác nhận đơn hàng.")]
    [InlineData("ten@example", "Email chưa đúng dạng. Ví dụ: ten@example.com")]
    [InlineData("ten example@a.vn", "Email chưa đúng dạng. Ví dụ: ten@example.com")]
    [InlineData("a@@b.vn", "Email chưa đúng dạng. Ví dụ: ten@example.com")]
    [InlineData("tenexample.com", "Email chưa đúng dạng. Ví dụ: ten@example.com")]
    [InlineData("ten@example.com", null)]
    public void An_email_needs_one_at_sign_and_a_dot_after_it(string typed, string? message)
    {
        var form = Good();
        form.SenderEmail = typed;

        Assert.Equal(message, MessageFor(form, "sender-email"));
    }

    [Fact]
    public void Names_and_the_street_are_needed_and_have_a_length_limit()
    {
        var form = Good();
        form.RecipientName = "   ";
        form.SenderName = new string('a', CheckoutCheck.NameMax + 1);
        form.Street = new string('b', CheckoutCheck.StreetMax + 1);

        Assert.Equal("Nhập họ và tên người nhận.", MessageFor(form, "recipient-name"));
        Assert.Equal("Họ và tên dài tối đa 100 ký tự.", MessageFor(form, "sender-name"));
        Assert.Equal("Địa chỉ dài tối đa 300 ký tự.", MessageFor(form, "street"));

        form.SenderName = new string('a', CheckoutCheck.NameMax);
        form.Street = new string('b', CheckoutCheck.StreetMax);
        Assert.Null(MessageFor(form, "sender-name"));
        Assert.Null(MessageFor(form, "street"));
    }

    [Fact]
    public void A_ward_must_be_one_of_the_list_and_a_made_up_window_or_payment_is_refused()
    {
        var form = Good();
        form.Ward = 8;
        form.Window = (DeliveryWindow)9;
        form.Payment = (PaymentMethod)9;

        var errors = CheckoutCheck.Check(form, Wards, out var request);

        Assert.Equal(["ward", "window-morning", "payment-zalopay"], errors.Select(e => e.Field));
        Assert.Null(request);
        form.Ward = null;
        Assert.Equal("Chọn phường giao hàng.", MessageFor(form, "ward"));
    }
}
