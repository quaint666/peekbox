using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

using static Peekbox.Tests.TestDates;

namespace Peekbox.Tests;

// Placing an order (F48): OrderService.PlaceAsync, without the page around it. Seed prices: Trà sen 180.000 (expires
// 31/12/2027), Bánh pía mini 120.000 (30/04/2027), Bánh quy bơ 75.000 (10/01/2027). Every test puts the stock back.
[Collection(DatabaseCollection.Name)]
public class OrderPlacingTests(PeekboxFixture fixture)
{
    private static OrderRequest Request(int wardId, PaymentMethod payment = PaymentMethod.ZaloPay) => new(
        "Nguyễn Văn A", "0900000000", "ten@example.com", "Trần Thị B", "0900111111", wardId, "12 đường mẫu",
        DeliveryWindow.Morning, payment);

    private async Task<string> HamperAsync(DateOnly? delivery, string? packagingId, params (string Slug, int Quantity)[] lines)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(async db =>
        {
            var hamper = new Hamper
            {
                Token = token, BudgetVnd = 500_000, DeliveryDate = delivery, PackagingId = packagingId,
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

    private Task<Ward> WardAsync(int ring) =>
        fixture.WithDbAsync(db => db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == ring));

    private Task<Dictionary<string, int>> StockAsync() =>
        fixture.WithDbAsync(db => db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Stock));

    private Task SetStockAsync(string slug, int stock) =>
        fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == slug).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, stock)));

    private async Task<PlaceResult> PlaceAsync(string token, OrderRequest request)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OrderService>().PlaceAsync(token, request);
    }

    private Task<Order?> OrderAsync(string? orderToken) =>
        fixture.WithDbAsync(db => db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.Token == orderToken));

    private Task<int> OrdersOfAsync(string hamperToken) =>
        fixture.WithDbAsync(db => db.Orders.CountAsync(o => db.Hampers.Any(h => h.Token == hamperToken && h.OrderId == o.Id)));

    [Fact]
    public async Task An_order_is_saved_with_copies_of_everything_and_takes_the_stock()
    {
        var stock = await StockAsync();
        try
        {
            var token = await HamperAsync(Delivery, "hard-bow", ("tra-sen", 2), ("banh-pia", 1));
            var ward = await WardAsync(2);

            var result = await PlaceAsync(token, Request(ward.Id));

            Assert.Equal(PlaceOutcome.Placed, result.Outcome);
            var order = await OrderAsync(result.OrderToken);
            Assert.NotNull(order);
            Assert.Equal(43, order.Token.Length);
            Assert.Equal(OrderStatus.New, order.Status);
            Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);   // the ZaloPay stand-in
            Assert.Equal(480_000, order.GoodsVnd);                    // 2 × 180.000 + 120.000
            Assert.Equal("Hộp cứng có nơ", order.PackagingName);
            Assert.Equal(40_000, order.PackagingFeeVnd);
            Assert.Equal(35_000, order.DeliveryFeeVnd);               // ring 2
            Assert.Equal(555_000, order.TotalVnd);
            Assert.Equal(ward.Name, order.WardName);
            Assert.Equal(Delivery, order.DeliveryDate);
            Assert.Equal(DeliveryWindow.Morning, order.DeliveryWindow);
            Assert.Equal("0900000000", order.SenderPhone);
            Assert.Equal(
                [("Trà sen", 180_000L, 2), ("Bánh pía mini", 120_000L, 1)],
                order.Items.OrderByDescending(i => i.UnitPriceVnd).Select(i => (i.Name, i.UnitPriceVnd, i.Quantity)));

            var after = await StockAsync();
            Assert.Equal(stock["tra-sen"] - 2, after["tra-sen"]);
            Assert.Equal(stock["banh-pia"] - 1, after["banh-pia"]);

            // The hamper is finished: the cart is empty and the builder starts a new hamper.
            using var scope = fixture.Factory.Services.CreateScope();
            var hampers = scope.ServiceProvider.GetRequiredService<HamperService>();
            Assert.Null(await hampers.FindAsync(token));
            Assert.Equal(0, await hampers.CartCountAsync(token));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task Cash_on_delivery_is_not_paid_yet()
    {
        var stock = await StockAsync();
        try
        {
            var token = await HamperAsync(Delivery, null, ("tra-sen", 1));

            var result = await PlaceAsync(token, Request((await WardAsync(1)).Id, PaymentMethod.CashOnDelivery));

            var order = await OrderAsync(result.OrderToken);
            Assert.NotNull(order);
            Assert.Equal(PaymentMethod.CashOnDelivery, order.PaymentMethod);
            Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
            Assert.Equal("Hộp giấy kraft", order.PackagingName);   // nothing picked: the first packaging, as the box shows
            Assert.Equal(205_000, order.TotalVnd);                  // 180.000 + 0 + 25.000 (ring 1)
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task Placing_the_same_hamper_again_opens_the_same_order()
    {
        var stock = await StockAsync();
        try
        {
            var token = await HamperAsync(Delivery, null, ("tra-sen", 1));
            var request = Request((await WardAsync(1)).Id);

            var first = await PlaceAsync(token, request);
            var second = await PlaceAsync(token, request);

            Assert.Equal(PlaceOutcome.Placed, first.Outcome);
            Assert.Equal(PlaceOutcome.AlreadyPlaced, second.Outcome);
            Assert.Equal(first.OrderToken, second.OrderToken);
            Assert.Equal(1, await OrdersOfAsync(token));
            Assert.Equal(stock["tra-sen"] - 1, (await StockAsync())["tra-sen"]);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    // A double click sends two requests at the same moment: the claim on the hamper row makes the second one wait.
    [Fact]
    public async Task Two_requests_at_the_same_moment_make_one_order()
    {
        var stock = await StockAsync();
        try
        {
            var token = await HamperAsync(Delivery, null, ("tra-sen", 1), ("banh-pia", 2));
            var request = Request((await WardAsync(1)).Id);

            var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => PlaceAsync(token, request))));

            Assert.Single(results, r => r.Outcome == PlaceOutcome.Placed);
            Assert.All(results, r => Assert.Contains(r.Outcome, new[] { PlaceOutcome.Placed, PlaceOutcome.AlreadyPlaced }));
            Assert.Single(results.Select(r => r.OrderToken).Distinct());
            Assert.Equal(1, await OrdersOfAsync(token));
            var after = await StockAsync();
            Assert.Equal(stock["tra-sen"] - 1, after["tra-sen"]);
            Assert.Equal(stock["banh-pia"] - 2, after["banh-pia"]);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    // Two visitors want the last two: the first takes them, the second gets the sold-out notice and nothing is taken.
    [Fact]
    public async Task When_an_item_runs_short_nothing_is_taken_and_the_hamper_is_lowered()
    {
        var stock = await StockAsync();
        try
        {
            await SetStockAsync("banh-pia", 2);
            var first = await HamperAsync(Delivery, null, ("banh-pia", 2));
            var second = await HamperAsync(Delivery, null, ("tra-sen", 1), ("banh-pia", 2));
            var third = await HamperAsync(Delivery, null, ("tra-sen", 1), ("banh-pia", 1));
            var ward = (await WardAsync(1)).Id;
            Assert.Equal(PlaceOutcome.Placed, (await PlaceAsync(first, Request(ward))).Outcome);

            var result = await PlaceAsync(second, Request(ward));

            Assert.Equal(PlaceOutcome.SoldOut, result.Outcome);
            Assert.Null(result.OrderToken);
            Assert.Equal([new SoldOutLine("Bánh pía mini", 2, 0)], result.SoldOut);
            Assert.Equal(0, await OrdersOfAsync(second));
            var after = await StockAsync();
            Assert.Equal(stock["tra-sen"], after["tra-sen"]);   // rolled back, though it came first in id order or not
            Assert.Equal(0, after["banh-pia"]);
            var lines = await fixture.WithDbAsync(db => db.HamperItems.Where(l => l.Hamper!.Token == second).Select(l => l.Product!.Slug).ToListAsync());
            Assert.Equal(["tra-sen"], lines);                   // the sold-out item left the hamper; the rest stays

            // Some left, but fewer than wanted: the hamper keeps what is left.
            await SetStockAsync("banh-pia", 1);
            var fourth = await HamperAsync(Delivery, null, ("banh-pia", 3));
            var lowered = await PlaceAsync(fourth, Request(ward));
            Assert.Equal([new SoldOutLine("Bánh pía mini", 3, 1)], lowered.SoldOut);
            var kept = await fixture.WithDbAsync(db => db.HamperItems.Where(l => l.Hamper!.Token == fourth).Select(l => l.Quantity).SingleAsync());
            Assert.Equal(1, kept);
            Assert.Equal(PlaceOutcome.Placed, (await PlaceAsync(third, Request(ward))).Outcome);
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }

    [Fact]
    public async Task A_box_that_cannot_go_on_is_not_placed()
    {
        var stock = await StockAsync();
        try
        {
            var ward = (await WardAsync(1)).Id;
            var empty = await HamperAsync(Delivery, null);
            var passed = await HamperAsync(TodayInVietnam, null, ("tra-sen", 1));
            var noDate = await HamperAsync(null, null, ("tra-sen", 1));
            // Bánh quy bơ expires 10/01/2027: out of date for 05/01/2027 with the 7-day margin.
            var outOfDate = await HamperAsync(new DateOnly(2027, 1, 5), null, ("banh-quy-bo", 1));

            Assert.Equal(PlaceOutcome.NotReady, (await PlaceAsync(empty, Request(ward))).Outcome);
            Assert.Equal(PlaceOutcome.NotReady, (await PlaceAsync(passed, Request(ward))).Outcome);
            Assert.Equal(PlaceOutcome.NotReady, (await PlaceAsync(noDate, Request(ward))).Outcome);
            if (TodayInVietnam < new DateOnly(2027, 1, 4))
            {
                Assert.Equal(PlaceOutcome.NotReady, (await PlaceAsync(outOfDate, Request(ward))).Outcome);
            }

            Assert.Equal(PlaceOutcome.UnknownWard, (await PlaceAsync(passed, Request(int.MaxValue))).Outcome);
            Assert.Equal(PlaceOutcome.NoHamper, (await PlaceAsync("not-a-hamper", Request(ward))).Outcome);
            Assert.Equal(PlaceOutcome.NoHamper, (await PlaceAsync(new string('a', 65), Request(ward))).Outcome);
            Assert.Equal(stock, await StockAsync());
            Assert.Equal(0, await fixture.WithDbAsync(db => db.Hampers.CountAsync(h => h.OrderId != null && new[] { empty, passed, noDate, outOfDate }.Contains(h.Token))));
        }
        finally
        {
            await TestOrders.RestoreStockAsync(fixture, stock);
        }
    }
}
