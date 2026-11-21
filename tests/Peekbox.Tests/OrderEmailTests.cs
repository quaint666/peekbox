using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Peekbox.Web.Data;
using Peekbox.Web.Services;
using Peekbox.Web.Services.Email;

using static Peekbox.Tests.TestDates;

namespace Peekbox.Tests;

// The confirmation email (F61): its words, and when the checkout sends it. The sender is a stand-in that writes to the log, so
// the checks that need to see an email swap in one that keeps them.
public class OrderEmailBuildTests
{
    private static readonly ShopInfo Shop = new("0900 000 001", "hello@peekbox.example", "15 Lê Lợi", "7:00 – 21:30", "https://zalo.me/0900000001");

    private static Order SomeOrder() => new()
    {
        Id = 123, Token = "tok-123", Status = OrderStatus.New, PaymentMethod = PaymentMethod.CashOnDelivery, PaymentStatus = PaymentStatus.Pending,
        SenderName = "Nguyễn Văn A", SenderPhone = "0900000000", SenderEmail = "ten@example.com",
        RecipientName = "Trần Thị B", RecipientPhone = "0900111111", WardName = "Phường Bến Nghé", Street = "12 đường mẫu",
        DeliveryDate = new DateOnly(2027, 1, 15), DeliveryWindow = DeliveryWindow.Morning,
        GoodsVnd = 400_000, PackagingName = "Hộp cứng có nơ", PackagingFeeVnd = 40_000, DeliveryFeeVnd = 25_000, TotalVnd = 465_000,
        Items =
        [
            new OrderItem { Name = "Trà sen", Brand = "A", Origin = "Hà Nội", UnitPriceVnd = 180_000, Quantity = 2 },
            new OrderItem { Name = "Bánh quy bơ", Brand = "D", Origin = "Đồng Nai", UnitPriceVnd = 40_000, Quantity = 1 },
        ],
    };

    [Fact]
    public void The_email_says_what_was_sold_what_it_cost_where_and_when_with_the_link_to_the_order()
    {
        var mail = OrderEmail.Build(SomeOrder(), "https://peekbox.example/", Shop);

        Assert.Equal(("ten@example.com", "Đơn hàng PEEK000123 đã được ghi nhận"), (mail.To, mail.Subject));
        foreach (var line in new[]
        {
            "Xin chào Nguyễn Văn A,",
            "Đơn hàng PEEK000123 đã được ghi nhận.",
            "- Trà sen × 2: 360.000đ",
            "- Bánh quy bơ: 40.000đ",
            "Tiền hàng: 400.000đ",
            "Đóng gói (Hộp cứng có nơ): 40.000đ",
            "Phí giao hàng: 25.000đ",
            "Tổng cộng: 465.000đ",
            "Giao ngày: 15/01/2027, sáng 8:00 – 12:00",
            "Người nhận: Trần Thị B, 0900 111 111",
            "Địa chỉ: 12 đường mẫu, Phường Bến Nghé",
            "Thanh toán: Trả tiền mặt khi nhận hàng, chưa thu tiền",
            "https://peekbox.example/orders/tok-123",   // one slash, although the origin ended with one
            "Cần giúp? Gọi 0900 000 001 hoặc viết tới hello@peekbox.example (hỗ trợ 7:00 – 21:30).",
        })
        {
            Assert.Contains(line, mail.Body);
        }

        Assert.DoesNotContain("Mã ", mail.Body);
        Assert.DoesNotContain("Lời nhắn", mail.Body);
        Assert.DoesNotContain("Gói quà", mail.Body);
        Assert.DoesNotContain("//orders", mail.Body);
    }

    [Fact]
    public void A_code_a_card_message_and_hidden_prices_are_in_the_email_and_the_prices_stay()
    {
        var order = SomeOrder();
        order.CouponCode = "CHAO10";
        order.DiscountVnd = 40_000;
        order.TotalVnd = 425_000;
        order.CardMessage = "Chúc mừng\nsinh nhật!";
        order.HidePrices = true;

        var body = OrderEmail.Build(order, "https://peekbox.example", Shop).Body;

        Assert.Contains("Mã CHAO10: −40.000đ", body);
        Assert.Contains("Tổng cộng: 425.000đ", body);
        Assert.Contains("Lời nhắn trên thiệp: Chúc mừng\nsinh nhật!", body);
        Assert.Contains("Gói quà: giá được ẩn bên trong hộp quà.", body);
        Assert.Contains("- Trà sen × 2: 360.000đ", body);   // the buyer still sees the prices (decision 2026-11-11)
    }

    [Fact]
    public void A_failed_payment_is_said_plainly()
    {
        var order = SomeOrder();
        order.PaymentMethod = PaymentMethod.ZaloPay;
        order.PaymentStatus = PaymentStatus.Failed;

        Assert.Contains("Thanh toán: Thanh toán ZaloPay chưa thành công", OrderEmail.Build(order, "https://peekbox.example", Shop).Body);
    }

    private sealed class ListLogger : ILogger<LogEmailSender>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add($"{logLevel}: {formatter(state, exception)}");
    }

    [Fact]
    public async Task The_stand_in_writes_the_email_to_the_log_and_sends_nothing()
    {
        var logger = new ListLogger();

        await new LogEmailSender(logger).SendAsync(new EmailMessage("ten@example.com", "Chủ đề", "Nội dung\ndòng hai"));

        var line = Assert.Single(logger.Lines);
        Assert.StartsWith("Information: Email (stand-in, not sent) to ten@example.com: Chủ đề", line);
        Assert.Contains("Nội dung\ndòng hai", line);
    }
}

[Collection(DatabaseCollection.Name)]
public class OrderEmailSendingTests(PeekboxFixture fixture)
{
    private sealed class KeptEmails : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            lock (Sent)
            {
                Sent.Add(message);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class BrokenSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => throw new InvalidOperationException("the mail server is down");
    }

    private WebApplicationFactory<Program> With(IEmailSender sender, params (string Key, string Value)[] settings) =>
        fixture.Factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton(sender);
            });
        });

    private async Task<string> HamperAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(async db =>
        {
            var hamper = new Hamper
            {
                Token = token, BudgetVnd = 500_000, DeliveryDate = Delivery.AddDays(70), PackagingId = "hard-bow",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            };
            hamper.Items.Add(new HamperItem { ProductId = (await db.Products.SingleAsync(p => p.Slug == "tra-sen")).Id, Quantity = 1 });
            db.Hampers.Add(hamper);
            return await db.SaveChangesAsync();
        });
        return token;
    }

    private async Task<(Visitor Visitor, Dictionary<string, string> Form)> ReadyAsync(WebApplicationFactory<Program> factory, string? email = "ten@example.com")
    {
        var visitor = new Visitor(fixture.Client(factory), await HamperAsync());
        await visitor.PageAsync("/checkout");
        var ward = await fixture.WithDbAsync(db => db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == 1));
        return (visitor, new Dictionary<string, string>
        {
            ["senderName"] = "Nguyễn Văn A", ["senderPhone"] = "0900000000", ["senderEmail"] = email ?? "",
            ["recipientName"] = "Trần Thị B", ["recipientPhone"] = "0900111111", ["ward"] = ward.Id.ToString(CultureInfo.InvariantCulture),
            ["street"] = "12 đường mẫu", ["window"] = "Evening", ["payment"] = "CashOnDelivery",
        });
    }

    private async Task<Dictionary<string, int>> StockAsync() =>
        await fixture.WithDbAsync(db => db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Stock));

    [Fact]
    public async Task A_placed_order_sends_one_email_to_the_buyer_with_the_link_and_a_second_click_sends_none()
    {
        var stock = await StockAsync();
        try
        {
            var kept = new KeptEmails();
            await using var factory = With(kept);
            var (visitor, form) = await ReadyAsync(factory);

            var first = await visitor.PostAsync("/checkout", new Dictionary<string, string>(form));
            var second = await visitor.PostAsync("/checkout", new Dictionary<string, string>(form));

            var location = first.Headers.Location?.OriginalString;
            Assert.Equal(location, second.Headers.Location?.OriginalString);
            var mail = Assert.Single(kept.Sent);
            Assert.Equal("ten@example.com", mail.To);
            Assert.Matches("^Đơn hàng PEEK[0-9]{6} đã được ghi nhận$", mail.Subject);
            Assert.Contains($"http://localhost{location}", mail.Body);   // the test server's own address, and the page the checkout opened
            Assert.Contains("Tổng cộng: 245.000đ", mail.Body);   // 180.000 + 40.000 packaging + 25.000 delivery
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task A_double_click_that_reaches_the_server_twice_at_once_still_sends_one_email_for_the_order()
    {
        var stock = await StockAsync();
        try
        {
            var kept = new KeptEmails();
            await using var factory = With(kept);
            const int boxes = 8;
            for (var i = 0; i < boxes; i++)
            {
                var (visitor, form) = await ReadyAsync(factory);
                var both = await Task.WhenAll(
                    visitor.PostAsync("/checkout", new Dictionary<string, string>(form)),
                    visitor.PostAsync("/checkout", new Dictionary<string, string>(form)));
                Assert.Equal(both[0].Headers.Location?.OriginalString, both[1].Headers.Location?.OriginalString);   // one order
            }

            Assert.Equal(boxes, kept.Sent.Count);   // not one per click
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task A_form_with_mistakes_sends_no_email()
    {
        var kept = new KeptEmails();
        await using var factory = With(kept);
        var (visitor, form) = await ReadyAsync(factory, email: "not an email");

        var response = await visitor.PostAsync("/checkout", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(kept.Sent);
    }

    [Fact]
    public async Task A_sender_that_fails_does_not_fail_the_order()
    {
        var stock = await StockAsync();
        try
        {
            await using var factory = With(new BrokenSender());
            var (visitor, form) = await ReadyAsync(factory);

            var response = await visitor.PostAsync("/checkout", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location?.OriginalString;
            Assert.Matches("^/orders/[A-Za-z0-9_-]{43}$", location);
            Assert.Equal(1, await fixture.WithDbAsync(db => db.Orders.CountAsync(o => "/orders/" + o.Token == location)));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task An_order_whose_zalopay_payment_failed_still_gets_its_email_and_says_so()
    {
        var stock = await StockAsync();
        try
        {
            var kept = new KeptEmails();
            await using var factory = With(kept, ("Payment:SimulateFailure", "true"));
            var (visitor, form) = await ReadyAsync(factory);
            form["payment"] = "ZaloPay";

            var response = await visitor.PostAsync("/checkout", form);

            Assert.EndsWith("/payment", response.Headers.Location?.OriginalString);
            var mail = Assert.Single(kept.Sent);
            Assert.Contains("Thanh toán: Thanh toán ZaloPay chưa thành công", mail.Body);
            Assert.Contains("http://localhost/orders/", mail.Body);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }
}
