using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// How long an unfinished hamper lives (decision 2026-11-21): the cookie starts over with every change of the box, the row
// is deleted a little after the cookie could no longer reach it, and one address cannot start hampers without limit.
[Collection(DatabaseCollection.Name)]
public class HamperLifecycleTests(PeekboxFixture fixture)
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static string Date => TestDates.TodayInVietnam.AddDays(20).ToString("yyyy-MM-dd");

    private static void AssertRenewed(Visitor visitor, string what)
    {
        var line = visitor.LastSetCookies.SingleOrDefault(c => c.StartsWith($"{HamperCookie.Name}="));
        Assert.True(line is not null, $"{what} did not write the hamper cookie again");
        Assert.Contains($"max-age={(int)HamperCookie.Lifetime.TotalSeconds}", line!.ToLowerInvariant());
    }

    // ---- the cookie starts over with every change of the box ----

    [Fact]
    public async Task Every_change_of_the_box_writes_the_cookie_again()
    {
        var visitor = new Visitor(fixture.Client());
        var packaging = await fixture.WithDbAsync(db => db.Packagings.OrderBy(p => p.SortOrder).Select(p => p.Id).FirstAsync());

        await visitor.PostAsync("/builder", new() { ["budget"] = "500000", ["deliveryDate"] = Date });
        AssertRenewed(visitor, "saving the budget");

        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen", ["quantity"] = "1" });
        AssertRenewed(visitor, "adding an item");

        await visitor.PostAsync("/builder/packaging", new() { ["packaging"] = packaging });
        AssertRenewed(visitor, "choosing the packaging");

        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen", ["quantity"] = "0" });
        AssertRenewed(visitor, "taking an item out");

        await visitor.PostAsync("/cart/clear", []);
        AssertRenewed(visitor, "emptying the box");

        await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "an-khang", ["from"] = "tet", ["replace"] = "true" });
        AssertRenewed(visitor, "\"Dùng hộp này\"");
    }

    // ---- the purge ----

    private static Hamper Row(string token, DateTimeOffset updated, int productId) => new()
    {
        Token = token, BudgetVnd = 300_000, CreatedAt = updated, UpdatedAt = updated,
        Items = [new HamperItem { ProductId = productId, Quantity = 1 }],
    };

    [Fact]
    public async Task The_purge_deletes_unfinished_hampers_a_month_past_their_cookie_and_keeps_the_rest()
    {
        var now = new DateTimeOffset(2027, 6, 1, 3, 0, 0, TimeSpan.Zero);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var prefix = $"purge-{tag}-";
        string Token(string name) => prefix + name;

        var deleted = await fixture.WithDbAsync(async db =>
        {
            var productId = await db.Products.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
            var ward = await db.Wards.OrderBy(w => w.SortOrder).FirstAsync();
            var order = new Order
            {
                Token = Guid.NewGuid().ToString("N"), SenderName = "A", SenderPhone = "0900000000", SenderEmail = "a@example.com",
                RecipientName = "B", RecipientPhone = "0900111111", WardId = ward.Id, WardName = ward.Name, Street = "12 đường mẫu",
                DeliveryDate = new DateOnly(2027, 1, 15), DeliveryWindow = DeliveryWindow.Morning, GoodsVnd = 100_000,
                PackagingName = "Hộp giấy kraft", PackagingFeeVnd = 0, DeliveryFeeVnd = 0, TotalVnd = 100_000, CreatedAt = now,
                Items = [new OrderItem { ProductId = productId, Name = "x", Brand = "y", Origin = "z", WeightGrams = 100, ExpiryDate = new DateOnly(2028, 1, 1), UnitPriceVnd = 100_000, Quantity = 1 }],
            };
            var ordered = Row(Token("ordered-old"), now - HamperRetention.Keep - TimeSpan.FromDays(300), productId);
            ordered.Order = order;
            db.Hampers.AddRange(
                Row(Token("old"), now - HamperRetention.Keep - TimeSpan.FromDays(1), productId),
                Row(Token("just-inside"), now - HamperRetention.Keep + TimeSpan.FromDays(1), productId),
                Row(Token("cookie-still-valid"), now - HamperCookie.Lifetime + TimeSpan.FromDays(1), productId),
                ordered);
            await db.SaveChangesAsync();

            return await new HamperRetention(db, new FixedClock(now)).PurgeAsync();
        });

        var left = await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token.StartsWith(prefix)).Select(h => h.Token).ToListAsync());
        Assert.Equal(1, deleted);
        Assert.Equal([Token("cookie-still-valid"), Token("just-inside"), Token("ordered-old")], left.Order().ToList());

        // The deleted hamper's items went with it, and the finished one still has its own.
        var orphans = await fixture.WithDbAsync(db => db.HamperItems.CountAsync(l => l.Hamper == null));
        Assert.Equal(0, orphans);
        var orderedItems = await fixture.WithDbAsync(db => db.HamperItems.CountAsync(l => l.Hamper!.Token == prefix + "ordered-old"));
        Assert.Equal(1, orderedItems);
    }

    [Fact]
    public void A_row_is_kept_30_days_longer_than_the_cookie_lives()
    {
        Assert.Equal(TimeSpan.FromDays(210), HamperRetention.Keep);
        Assert.Equal(TimeSpan.FromDays(180), HamperCookie.Lifetime);
    }

    [Fact]
    public void The_site_runs_the_purge_every_day()
    {
        Assert.Contains(fixture.Factory.Services.GetServices<IHostedService>(), s => s is HamperPurgeService);
        Assert.Equal(TimeSpan.FromDays(1), HamperPurgeService.Every);
    }

    [Fact]
    public async Task A_failing_purge_is_logged_and_never_stops_the_site()
    {
        var broken = new ServiceCollection().AddScoped<HamperRetention>(_ => throw new InvalidOperationException("database down")).BuildServiceProvider();
        var service = new HamperPurgeService(broken.GetRequiredService<IServiceScopeFactory>(), NullLogger<HamperPurgeService>.Instance);

        await service.PurgeOnceAsync(CancellationToken.None);   // must not throw
    }

    // ---- one address cannot start hampers without limit ----

    private WebApplicationFactory<Program> Limited(int perWindow) =>
        fixture.Factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:NewHampersPer15Minutes", perWindow.ToString()));

    [Fact]
    public async Task The_third_new_hamper_from_one_address_in_the_window_gets_the_429_page_and_a_visitor_with_a_hamper_is_not_counted()
    {
        await using var limited = Limited(2);
        var first = new Visitor(fixture.Client(limited));
        Assert.Equal(HttpStatusCode.Redirect, (await first.PostAsync("/builder", new() { ["budget"] = "300000" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await new Visitor(fixture.Client(limited)).PostAsync("/builder", new() { ["budget"] = "300000" })).StatusCode);

        var refused = await new Visitor(fixture.Client(limited)).PostAsync("/builder", new() { ["budget"] = "300000" });
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("900", refused.Headers.RetryAfter?.Delta?.TotalSeconds.ToString());
        Assert.Contains("Bạn thao tác hơi nhanh", await refused.Content.ReadAsStringAsync());

        // The first visitor already has a hamper: changing it creates nothing, so the limit does not touch it.
        Assert.Equal(HttpStatusCode.Redirect, (await first.PostAsync("/builder", new() { ["budget"] = "400000" })).StatusCode);
    }

    [Fact]
    public async Task Using_a_ready_hamper_without_a_hamper_of_ones_own_counts_as_a_new_hamper_too()
    {
        await using var limited = Limited(1);
        Assert.Equal(HttpStatusCode.Redirect, (await new Visitor(fixture.Client(limited)).PostAsync("/dip/dung-hop", new() { ["slug"] = "an-khang", ["from"] = "tet" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await new Visitor(fixture.Client(limited)).PostAsync("/dip/dung-hop", new() { ["slug"] = "an-khang", ["from"] = "tet" })).StatusCode);
    }
}
