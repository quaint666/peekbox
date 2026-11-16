using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// Orders for the order page and the track page, placed through OrderService. Seed: Trà sen 180.000, hard box with bow
// 40.000, the first ring-1 ward. Sender: Nguyễn Văn A, 0900000000. Callers put the stock back.
internal static class TestOrders
{
    // Places a hamper of Trà sen × quantity and gives the order's token, with the stock as it was before.
    public static async Task<(string Token, Ward Ward, Dictionary<string, int> Stock)> PlaceAsync(PeekboxFixture fixture, int quantity, PaymentMethod payment)
    {
        var stock = await fixture.WithDbAsync(db => db.Products.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Stock));
        var ward = await fixture.WithDbAsync(db => db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).FirstAsync(w => w.Ring == 1));
        var hamperToken = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(async db =>
        {
            var hamper = new Hamper
            {
                Token = hamperToken, BudgetVnd = 500_000, DeliveryDate = TestDates.Delivery, PackagingId = "hard-bow",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            };
            hamper.Items.Add(new HamperItem { ProductId = (await db.Products.SingleAsync(p => p.Slug == "tra-sen")).Id, Quantity = quantity });
            db.Hampers.Add(hamper);
            return await db.SaveChangesAsync();
        });
        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<OrderService>().PlaceAsync(hamperToken, new OrderRequest(
            "Nguyễn Văn A", "0900000000", "ten@example.com", "Trần Thị B", "0900111111", ward.Id, "12 đường mẫu", DeliveryWindow.Afternoon, payment));
        Assert.Equal(PlaceOutcome.Placed, result.Outcome);
        return (result.OrderToken!, ward, stock);
    }

    public static Task RestoreStockAsync(PeekboxFixture fixture, Dictionary<string, int> stock) => fixture.WithDbAsync(async db =>
    {
        foreach (var product in await db.Products.ToListAsync())
        {
            product.Stock = stock[product.Slug];
        }

        return await db.SaveChangesAsync();
    });
}
