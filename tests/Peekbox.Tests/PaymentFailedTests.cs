using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

using static Peekbox.Tests.TestDates;

namespace Peekbox.Tests;

// F49, the payment-failed page: /orders/{token}/payment and its two POSTs. ZaloPay is a stand-in; the test switch
// "Payment:SimulateFailure" makes it fail. Seed: Trà sen 180.000, hard box with bow 40.000. Every test puts the stock back.
[Collection(DatabaseCollection.Name)]
public class PaymentFailedTests(PeekboxFixture fixture)
{
    private WebApplicationFactory<Program> Failing() =>
        fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Payment:SimulateFailure", "true"));

    private async Task<string> HamperAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(async db =>
        {
            var hamper = new Hamper
            {
                Token = token, BudgetVnd = 500_000, DeliveryDate = Delivery.AddDays(50), PackagingId = "hard-bow",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            };
            hamper.Items.Add(new HamperItem { ProductId = (await db.Products.SingleAsync(p => p.Slug == "tra-sen")).Id, Quantity = 1 });
            db.Hampers.Add(hamper);
            return await db.SaveChangesAsync();
        });
        return token;
    }

    // Checks out with ZaloPay on a site whose switch is on: gives the visitor and where the checkout sent them.
    private async Task<(Visitor Visitor, string Location)> FailedCheckoutAsync(WebApplicationFactory<Program> failing, string payment = "ZaloPay")
    {
        var visitor = new Visitor(fixture.Client(failing), await HamperAsync());
        await visitor.PageAsync("/checkout");
        var ward = await fixture.WithDbAsync(db => db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == 1));
        var response = await visitor.PostAsync("/checkout", new Dictionary<string, string>
        {
            ["senderName"] = "Nguyễn Văn A", ["senderPhone"] = "0900000000", ["senderEmail"] = "ten@example.com",
            ["recipientName"] = "Trần Thị B", ["recipientPhone"] = "0900111111", ["ward"] = ward.Id.ToString(CultureInfo.InvariantCulture),
            ["street"] = "12 đường mẫu", ["window"] = "Morning", ["payment"] = payment,
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return (visitor, response.Headers.Location!.OriginalString);
    }

    private Task<Order> OrderAsync(string orderPath) =>
        fixture.WithDbAsync(db => db.Orders.AsNoTracking().SingleAsync(o => "/orders/" + o.Token == orderPath));

    private static string OrderPath(string paymentPath) => paymentPath[..paymentPath.LastIndexOf("/payment", StringComparison.Ordinal)];

    private async Task<Dictionary<string, int>> StockAsync() =>
        await fixture.WithDbAsync(db => db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Stock));

    [Fact]
    public async Task A_failed_zalopay_payment_keeps_the_order_and_opens_the_payment_failed_page()
    {
        var stock = await StockAsync();
        try
        {
            await using var failing = Failing();
            var (visitor, location) = await FailedCheckoutAsync(failing);

            Assert.Matches("^/orders/[A-Za-z0-9_-]{43}/payment$", location);
            var order = await OrderAsync(OrderPath(location));
            Assert.Equal((PaymentMethod.ZaloPay, PaymentStatus.Failed, OrderStatus.New), (order.PaymentMethod, order.PaymentStatus, order.Status));
            Assert.Equal(stock["tra-sen"] - 1, (await StockAsync())["tra-sen"]);   // the box is kept: its stock stays taken

            var html = await visitor.PageAsync(location);
            Assert.Contains("Thanh toán chưa thành công</h1>", html);
            Assert.Contains($"Đơn hàng <strong>{order.Number}</strong> và hộp quà của bạn vẫn được giữ, chưa có khoản nào bị tính.", html);
            Assert.Contains($"action=\"{location}/retry\"", html);
            Assert.Contains($"action=\"{location}/cash\"", html);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "name=\"__RequestVerificationToken\"").Count);
            BuilderItemsTests.AssertWellFormed("payment failed", html);

            // The order's own page says so too, and links here.
            var orderPage = await visitor.PageAsync(OrderPath(location));
            Assert.Contains("Thanh toán: <strong>Thanh toán ZaloPay chưa thành công</strong>", orderPage);
            Assert.Contains($"<a href=\"{location}\">Thử lại hoặc chọn thanh toán khi nhận hàng</a>", orderPage);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task A_retry_while_the_switch_is_on_fails_again_and_one_after_it_is_off_pays()
    {
        var stock = await StockAsync();
        try
        {
            await using var failing = Failing();
            var (visitor, location) = await FailedCheckoutAsync(failing);
            await visitor.PageAsync(location);

            var again = await visitor.PostAsync(location + "/retry", []);
            Assert.Equal(location, again.Headers.Location?.OriginalString);
            Assert.Contains("Lần thử lại này cũng chưa thành công.", await visitor.PageAsync(location));
            Assert.Equal(PaymentStatus.Failed, (await OrderAsync(OrderPath(location))).PaymentStatus);

            // The switch is off now (the normal site): the same retry goes through.
            var later = new Visitor(fixture.Client());
            await later.PageAsync(location);
            var paid = await later.PostAsync(location + "/retry", []);
            Assert.Equal(OrderPath(location), paid.Headers.Location?.OriginalString);
            Assert.Equal(PaymentStatus.Paid, (await OrderAsync(OrderPath(location))).PaymentStatus);
            Assert.Contains("Đã thanh toán qua ZaloPay (mô phỏng)", await later.PageAsync(OrderPath(location)));
            Assert.DoesNotContain("Thanh toán chưa thành công", await later.PageAsync(OrderPath(location)));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task Switching_to_cash_makes_the_same_order_cash_on_delivery_and_a_second_click_changes_nothing()
    {
        var stock = await StockAsync();
        try
        {
            await using var failing = Failing();
            var (visitor, location) = await FailedCheckoutAsync(failing);
            await visitor.PageAsync(location);
            var before = await OrderAsync(OrderPath(location));

            var first = await visitor.PostAsync(location + "/cash", []);
            var second = await visitor.PostAsync(location + "/cash", []);

            Assert.Equal(OrderPath(location), first.Headers.Location?.OriginalString);
            Assert.Equal(OrderPath(location), second.Headers.Location?.OriginalString);
            var after = await OrderAsync(OrderPath(location));
            Assert.Equal((before.Id, PaymentMethod.CashOnDelivery, PaymentStatus.Pending, before.TotalVnd), (after.Id, after.PaymentMethod, after.PaymentStatus, after.TotalVnd));
            Assert.Contains("Trả tiền mặt khi nhận hàng, chưa thu tiền", await visitor.PageAsync(OrderPath(location)));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task The_page_is_only_for_a_failed_zalopay_payment_of_an_order_that_is_on()
    {
        var stock = await StockAsync();
        try
        {
            var (token, _, _) = await TestOrders.PlaceAsync(fixture, 1, PaymentMethod.ZaloPay);   // paid at once
            var client = fixture.Client();

            Assert.Equal($"/orders/{token}", (await client.GetAsync($"/orders/{token}/payment")).Headers.Location?.OriginalString);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/orders/" + new string('a', 43) + "/payment")).StatusCode);

            // A cancelled failed order is not "failed": nothing to retry, and a retry changes nothing.
            await fixture.WithDbAsync(db => db.Orders.Where(o => o.Token == token).ExecuteUpdateAsync(s =>
                s.SetProperty(o => o.PaymentStatus, PaymentStatus.Failed).SetProperty(o => o.Status, OrderStatus.Cancelled)));
            var visitor = new Visitor(client);
            await visitor.PageAsync($"/orders/{token}");
            Assert.Equal($"/orders/{token}", (await client.GetAsync($"/orders/{token}/payment")).Headers.Location?.OriginalString);
            await visitor.PostAsync($"/orders/{token}/payment/cash", []);
            await visitor.PostAsync($"/orders/{token}/payment/retry", []);
            var order = await fixture.WithDbAsync(db => db.Orders.AsNoTracking().SingleAsync(o => o.Token == token));
            Assert.Equal((PaymentMethod.ZaloPay, PaymentStatus.Failed), (order.PaymentMethod, order.PaymentStatus));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task Cash_on_delivery_is_never_affected_by_the_switch_and_a_post_needs_the_form_token()
    {
        var stock = await StockAsync();
        try
        {
            await using var failing = Failing();
            var (visitor, location) = await FailedCheckoutAsync(failing, "CashOnDelivery");
            Assert.Matches("^/orders/[A-Za-z0-9_-]{43}$", location);   // straight to the order, no failed page
            Assert.Equal(PaymentStatus.Pending, (await OrderAsync(location)).PaymentStatus);

            Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync(location + "/payment/retry", [], withToken: false)).StatusCode);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }
}
