using Microsoft.EntityFrameworkCore;
using Npgsql;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// The data M3 adds: the delivery wards and their fees, the order number, and the checks the database keeps on an order.
[Collection(DatabaseCollection.Name)]
public class OrderTablesTests(PeekboxFixture fixture)
{
    [Fact]
    public async Task The_wards_are_seeded_from_the_file_in_its_order()
    {
        var file = SeedData.LoadWards(SeedData.WardsFilePath).Wards;
        var rows = await fixture.WithDbAsync(db => db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).ToListAsync());

        Assert.Equal(101, file.Count);
        Assert.Equal(file.Select(w => (w.Name, w.Ring)), rows.Select(w => (w.Name, w.Ring)));
    }

    [Fact]
    public void Every_ward_in_the_file_has_a_fee_and_a_unique_name()
    {
        var wards = SeedData.LoadWards(SeedData.WardsFilePath).Wards;

        Assert.Equal(wards.Count, wards.Select(w => w.Name).Distinct().Count());
        Assert.All(wards, w => Assert.True(DeliveryFees.ForRing(w.Ring) > 0, w.Name));
        Assert.Equal([1, 2, 3], wards.Select(w => w.Ring).Distinct().Order());
    }

    [Fact]
    public void The_fee_goes_up_with_the_ring_and_a_fourth_ring_has_none()
    {
        Assert.True(DeliveryFees.ForRing(1) < DeliveryFees.ForRing(2));
        Assert.True(DeliveryFees.ForRing(2) < DeliveryFees.ForRing(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeliveryFees.ForRing(4));
    }

    [Theory]
    [InlineData("PEEK000123", 123)]
    [InlineData("peek000123", 123)]
    [InlineData("  PEEK123 ", 123)]
    [InlineData("123", 123)]
    [InlineData("PEEK0", null)]
    [InlineData("PEEK-12", null)]
    [InlineData("PEEK１２３", null)]   // full-width digits are not ASCII digits
    [InlineData("PEEK9999999999", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_order_number_is_read_back_to_the_id(string? text, int? id)
    {
        Assert.Equal(id, OrderNumber.Parse(text));
    }

    [Fact]
    public void The_order_number_has_six_digits()
    {
        Assert.Equal("PEEK000123", OrderNumber.Format(123));
        Assert.Equal(123, OrderNumber.Parse(OrderNumber.Format(123)));
    }

    // The money rule and the phone rule are also checks in the database, so no code path can save a wrong order.
    [Theory]
    [InlineData(420_000, 25_000, 445_000, "0900000000", true)]
    [InlineData(420_000, 25_000, 444_000, "0900000000", false)]
    [InlineData(420_000, 25_000, 445_000, "090000000", false)]
    [InlineData(420_000, 25_000, 445_000, "1900000000", false)]
    public async Task The_database_refuses_a_wrong_total_or_phone(long goods, long delivery, long total, string phone, bool saved)
    {
        var error = await Record.ExceptionAsync(() => fixture.WithDbAsync(async db =>
        {
            var ward = await db.Wards.OrderBy(w => w.SortOrder).FirstAsync();
            var product = await db.Products.OrderBy(p => p.Id).FirstAsync();
            var order = new Order
            {
                Token = Guid.NewGuid().ToString("N"), SenderName = "A", SenderPhone = phone, SenderEmail = "a@example.com",
                RecipientName = "B", RecipientPhone = "0900111111", WardId = ward.Id, WardName = ward.Name, Street = "12 đường mẫu",
                DeliveryDate = new DateOnly(2027, 1, 15), DeliveryWindow = DeliveryWindow.Morning, GoodsVnd = goods,
                PackagingName = "Hộp giấy kraft", PackagingFeeVnd = 0, DeliveryFeeVnd = delivery, TotalVnd = total,
                CreatedAt = DateTimeOffset.UtcNow,
                Items =
                [
                    new OrderItem
                    {
                        ProductId = product.Id, Name = product.Name, Brand = product.Brand, Origin = product.Origin,
                        WeightGrams = product.WeightGrams, ExpiryDate = product.ExpiryDate, UnitPriceVnd = goods, Quantity = 1,
                    },
                ],
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            var stored = await db.Database.SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"Orders\" WHERE \"Id\" = {order.Id}").SingleAsync();
            Assert.Equal("New", stored);   // stored as text, not a number
            db.Orders.Remove(order);
            await db.SaveChangesAsync();
            return 0;
        }));

        if (saved)
        {
            Assert.Null(error);
        }
        else
        {
            var check = Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(error).InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, check.SqlState);
        }
    }
}
