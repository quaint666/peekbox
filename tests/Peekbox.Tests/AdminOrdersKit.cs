using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Security;

namespace Peekbox.Tests;

// Orders for the admin's orders screen, written straight into the database (placing one through the checkout is tested
// elsewhere). The money adds up as the database demands: total = goods + packaging + delivery - discount.
internal static class AdminOrdersKit
{
    private static int _next;

    // A delivery day of its own for each test, far from the days other tests use, so lists do not meet.
    public static DateOnly Day() => TestDates.TodayInVietnam.AddDays(200 + Interlocked.Increment(ref _next));

    public static async Task<int> OrderAsync(
        PeekboxFixture fixture, DateOnly day, OrderStatus status = OrderStatus.New, PaymentMethod method = PaymentMethod.ZaloPay,
        PaymentStatus payment = PaymentStatus.Paid, string recipient = "Trần Thị B", string sender = "Nguyễn Văn A", string recipientPhone = "0900111111",
        string senderPhone = "0900000000", DeliveryWindow window = DeliveryWindow.Morning, string? cardMessage = null, bool hidePrices = false,
        string? couponCode = null, long discount = 0, int quantity = 2, string? trackingCode = null, string? adminNote = null)
    {
        return await fixture.WithDbAsync(async db =>
        {
            var ward = await db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == 1);
            var product = await db.Products.AsNoTracking().SingleAsync(p => p.Slug == "tra-sen");
            var goods = product.PriceVnd * quantity;
            var order = new Order
            {
                Token = SecretToken.New(), Status = status, PaymentMethod = method, PaymentStatus = payment,
                SenderName = sender, SenderPhone = senderPhone, SenderEmail = "ten@example.com", RecipientName = recipient, RecipientPhone = recipientPhone,
                WardId = ward.Id, WardName = ward.Name, Street = "12 đường mẫu", DeliveryDate = day, DeliveryWindow = window,
                CardMessage = cardMessage, HidePrices = hidePrices, GoodsVnd = goods, PackagingName = "Hộp cứng có nơ", PackagingFeeVnd = 40_000,
                DeliveryFeeVnd = 25_000, CouponCode = couponCode, DiscountVnd = discount, TotalVnd = goods + 40_000 + 25_000 - discount,
                TrackingCode = trackingCode, AdminNote = adminNote, CreatedAt = DateTimeOffset.UtcNow,
                Items = [new OrderItem { ProductId = product.Id, Name = product.Name, Brand = product.Brand, Origin = product.Origin, WeightGrams = product.WeightGrams, ExpiryDate = product.ExpiryDate, UnitPriceVnd = product.PriceVnd, Quantity = quantity }],
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return order.Id;
        });
    }

    public static Task<Order> ReloadAsync(PeekboxFixture fixture, int id) =>
        fixture.WithDbAsync(db => db.Orders.AsNoTracking().SingleAsync(o => o.Id == id));

    public static string Key(OrderStatus status) => status.ToString().ToLowerInvariant();

    public static Task<Dictionary<string, int>> StockAsync(PeekboxFixture fixture) =>
        fixture.WithDbAsync(db => db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Stock));
}
