using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Formatting;
using Peekbox.Web.Models;
using Peekbox.Web.Services;

using static Peekbox.Tests.TestDates;

namespace Peekbox.Tests;

// An order's page (F51): /orders/{token}. Orders are placed through OrderService. Seed: Trà sen 180.000 (Thương hiệu A,
// 100 g, Hà Nội, expires 31/12/2027), hard box with bow 40.000; the first ring-1 ward costs 25.000. Every test puts
// the stock back.
[Collection(DatabaseCollection.Name)]
public class OrderPageTests(PeekboxFixture fixture)
{
    private Task<(string Token, Ward Ward, Dictionary<string, int> Stock)> PlaceAsync(int quantity, PaymentMethod payment) =>
        TestOrders.PlaceAsync(fixture, quantity, payment);

    private Task SetAsync(string token, OrderStatus status) =>
        fixture.WithDbAsync(db => db.Orders.Where(o => o.Token == token).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status)));

    private async Task<string> PageAsync(string token)
    {
        var response = await fixture.Client().GetAsync($"/orders/{token}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(Regex.Replace(await response.Content.ReadAsStringAsync(), @"\s+", " "));
    }

    private static string Steps(string html) => Regex.Match(html, @"<ol class=""order-steps"">[\s\S]*?</ol>").Value;

    [Fact]
    public async Task A_new_order_shows_its_number_status_items_delivery_and_sums()
    {
        var (token, ward, stock) = await PlaceAsync(2, PaymentMethod.ZaloPay);
        try
        {
            var number = await fixture.WithDbAsync(db => db.Orders.Where(o => o.Token == token).Select(o => o.Id).SingleAsync());
            var response = await fixture.Client().GetAsync($"/orders/{token}");
            var html = await PageAsync(token);

            Assert.Equal("no-store,no-cache", string.Join(",", response.Headers.CacheControl!.ToString().Split(", ")));
            Assert.Contains($"<title>Đơn hàng {OrderNumber.Format(number)} · Peekbox</title>", html);
            Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html);
            Assert.Contains("Cảm ơn bạn, đơn hàng đã được ghi nhận</h1>", html);
            Assert.Contains($"Mã đơn hàng: <strong>{OrderNumber.Format(number)}</strong>. Hãy lưu đường dẫn của trang này để xem lại đơn hàng.", html);
            Assert.Contains("Bạn cũng có thể dùng mục <a href=\"/track\">Tra cứu đơn hàng</a> với mã đơn hàng và số điện thoại người đặt.", html);
            Assert.DoesNotContain("đã gửi email", html);   // no email is sent yet (F61)

            var steps = Steps(html);
            Assert.Contains("<li class=\"order-steps__step order-steps__step--done\" aria-current=\"step\"><span class=\"order-steps__bar\"></span><span class=\"order-steps__label\">Mới</span></li>", steps);
            Assert.Contains("<li class=\"order-steps__step\"><span class=\"order-steps__bar\"></span><span class=\"order-steps__label\">Đã giao</span></li>", steps);
            Assert.Single(Regex.Matches(steps, "order-steps__step--done"));
            Assert.Contains("Thanh toán: <strong>Đã thanh toán qua ZaloPay (mô phỏng)</strong>", html);

            Assert.Contains("<span class=\"order-line__name\">Trà sen</span> <span class=\"order-line__meta meta\">Thương hiệu A · 100 g · Xuất xứ Hà Nội · HSD 12/2027</span>", html);
            Assert.Contains("<span class=\"order-line__each\">2 × 180.000đ</span> <strong>360.000đ</strong>", html);
            Assert.Contains("src=\"/images/products/tra-sen.webp\"", html);
            Assert.Contains("<li class=\"order-line order-line--packaging\"><span>Hộp cứng có nơ</span><strong>40.000đ</strong></li>", html);

            Assert.Contains("<div><dt>Người nhận:</dt> <dd>Trần Thị B, 0900 111 111</dd></div>", html);
            Assert.Contains($"<div><dt>Địa chỉ:</dt> <dd>12 đường mẫu, {ward.Name}</dd></div>", html);
            Assert.Contains($"<div><dt>Ngày giao:</dt> <dd>{VnDate.Day(Delivery)}, chiều 13:00 – 17:00</dd></div>", html);
            Assert.Contains("<div><dt>Tiền hàng</dt><dd>360.000đ</dd></div>", html);
            Assert.Contains("<div><dt>Đóng gói</dt><dd>40.000đ</dd></div>", html);
            Assert.Contains("<div><dt>Phí giao hàng</dt><dd>25.000đ</dd></div>", html);
            Assert.Contains("<div class=\"order-sums__total\"><dt>Tổng cộng</dt><dd>425.000đ</dd></div>", html);
            Assert.Contains("<a class=\"btn btn--primary\" href=\"/builder\">Tạo hộp quà khác</a>", html);
            Assert.Contains("<a class=\"btn btn--outline\" href=\"/\">Về trang chủ</a>", html);
            Assert.DoesNotContain("ten@example.com", html);   // the page shows nothing it does not need
            Assert.DoesNotContain("0900 000 000", html);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task The_page_shows_what_was_sold_even_after_the_product_changes()
    {
        var (token, _, stock) = await PlaceAsync(1, PaymentMethod.ZaloPay);
        var before = await fixture.WithDbAsync(db => db.Products.AsNoTracking().SingleAsync(p => p.Slug == "tra-sen"));
        try
        {
            await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "tra-sen")
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, "Trà sen mới").SetProperty(p => p.PriceVnd, 999_000)));

            var html = await PageAsync(token);

            Assert.Contains("<span class=\"order-line__name\">Trà sen</span>", html);
            Assert.Contains("<strong>180.000đ</strong>", html);
            Assert.DoesNotContain("999.000đ", html);
        }
        finally
        {
            await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "tra-sen")
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, before.Name).SetProperty(p => p.PriceVnd, before.PriceVnd)));
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task The_steps_reached_are_filled_and_the_current_one_is_marked()
    {
        var (token, _, stock) = await PlaceAsync(1, PaymentMethod.CashOnDelivery);
        try
        {
            await SetAsync(token, OrderStatus.Delivering);

            var html = await PageAsync(token);

            var steps = Steps(html);
            Assert.Equal(3, Regex.Matches(steps, "order-steps__step--done").Count);
            Assert.Contains("<li class=\"order-steps__step order-steps__step--done\" aria-current=\"step\"><span class=\"order-steps__bar\"></span><span class=\"order-steps__label\">Đang giao</span></li>", steps);
            Assert.Single(Regex.Matches(steps, "aria-current"));
            Assert.Contains("Thanh toán: <strong>Trả tiền mặt khi nhận hàng, chưa thu tiền</strong>", html);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task A_cancelled_order_says_so_and_has_no_steps()
    {
        var (token, _, stock) = await PlaceAsync(1, PaymentMethod.CashOnDelivery);
        try
        {
            await SetAsync(token, OrderStatus.Cancelled);

            var html = await PageAsync(token);

            Assert.Contains("Đơn hàng này đã bị huỷ</h1>", html);
            Assert.Contains("<section class=\"alert order-banner\" aria-labelledby=\"order-title\">", html);
            Assert.Contains("Đơn hàng sẽ không được giao. Cần giúp?", html);
            Assert.Contains("<p class=\"alert order-status__cancelled\">Đã huỷ</p>", html);
            Assert.DoesNotContain("order-steps", html);
            Assert.DoesNotContain("Cảm ơn bạn", html);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("1")]
    public async Task An_unknown_token_is_not_found(string token)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client().GetAsync($"/orders/{token}")).StatusCode);
    }

    [Fact]
    public async Task Every_state_of_the_order_page_is_well_formed()
    {
        var (token, _, stock) = await PlaceAsync(2, PaymentMethod.ZaloPay);
        try
        {
            BuilderItemsTests.AssertWellFormed("new", await (await fixture.Client().GetAsync($"/orders/{token}")).Content.ReadAsStringAsync());
            await SetAsync(token, OrderStatus.Cancelled);
            BuilderItemsTests.AssertWellFormed("cancelled", await (await fixture.Client().GetAsync($"/orders/{token}")).Content.ReadAsStringAsync());
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }
}

// What the page says about paying, the date and a phone number, for every value.
public class OrderPageModelTests
{
    private static OrderPageModel Model(PaymentMethod method, PaymentStatus status, DeliveryWindow window = DeliveryWindow.Morning) => new(
        new Order
        {
            Token = "t", PaymentMethod = method, PaymentStatus = status, SenderName = "", SenderPhone = "", SenderEmail = "",
            RecipientName = "", RecipientPhone = "", WardName = "", Street = "", PackagingName = "",
            DeliveryDate = new DateOnly(2027, 1, 15), DeliveryWindow = window,
        },
        new Dictionary<int, string>());

    [Theory]
    [InlineData(PaymentMethod.ZaloPay, PaymentStatus.Paid, "Đã thanh toán qua ZaloPay (mô phỏng)")]
    [InlineData(PaymentMethod.ZaloPay, PaymentStatus.Pending, "Chưa thanh toán qua ZaloPay")]
    [InlineData(PaymentMethod.ZaloPay, PaymentStatus.Failed, "Chưa thanh toán qua ZaloPay")]
    [InlineData(PaymentMethod.ZaloPay, PaymentStatus.Refunded, "Đã hoàn tiền qua ZaloPay")]
    [InlineData(PaymentMethod.CashOnDelivery, PaymentStatus.Pending, "Trả tiền mặt khi nhận hàng, chưa thu tiền")]
    [InlineData(PaymentMethod.CashOnDelivery, PaymentStatus.Failed, "Trả tiền mặt khi nhận hàng, chưa thu tiền")]
    [InlineData(PaymentMethod.CashOnDelivery, PaymentStatus.Paid, "Đã thu tiền mặt khi nhận hàng")]
    [InlineData(PaymentMethod.CashOnDelivery, PaymentStatus.Refunded, "Đã hoàn tiền")]
    public void Every_payment_has_its_words(PaymentMethod method, PaymentStatus status, string text) =>
        Assert.Equal(text, Model(method, status).PaymentText);

    [Theory]
    [InlineData(DeliveryWindow.Morning, "15/01/2027, sáng 8:00 – 12:00")]
    [InlineData(DeliveryWindow.Evening, "15/01/2027, tối 17:00 – 21:00")]
    public void The_date_and_window_read_as_one_phrase(DeliveryWindow window, string text) =>
        Assert.Equal(text, Model(PaymentMethod.ZaloPay, PaymentStatus.Paid, window).When);

    [Theory]
    [InlineData("0900111111", "0900 111 111")]
    [InlineData("090011111", "090011111")]
    public void A_phone_is_shown_in_groups_of_4_3_3(string digits, string text) =>
        Assert.Equal(text, OrderPageModel.Phone(digits));
}
