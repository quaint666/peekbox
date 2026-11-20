using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// The ready hampers in content/data/hampers.json (F15, decision 2026-11-17), checked against the rules of the occasion
// pages, against the catalogue, and against what the database and ReadyHampers give back.
[Collection(DatabaseCollection.Name)]
public class ReadyHamperTests(PeekboxFixture fixture)
{
    private static readonly SeedHampersFile File = SeedData.LoadHampers(SeedData.HampersFilePath);

    private async Task<T> WithServiceAsync<T>(Func<ReadyHampers, Task<T>> action)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ReadyHampers>());
    }

    [Fact]
    public void Slugs_are_unique_and_every_budget_is_one_of_the_seven()
    {
        Assert.Equal(File.Hampers.Count, File.Hampers.Select(h => h.Slug).Distinct().Count());
        foreach (var h in File.Hampers)
        {
            Assert.True(BudgetTiers.All.Contains(h.BudgetVnd), $"{h.Slug}: {h.BudgetVnd} is not a budget tier");
            Assert.NotEmpty(h.Items);
            Assert.NotEmpty(h.Occasions);
            Assert.StartsWith("/images/hampers/", h.Image);
        }
    }

    [Fact]
    public void Every_hamper_photo_is_a_file_in_wwwroot()
    {
        foreach (var image in File.Hampers.Select(h => h.Image).Concat(File.Occasions.Select(o => o.Image)))
        {
            var path = Path.Combine(RepoRoot.WebProject, "wwwroot", image.TrimStart('/'));
            Assert.True(System.IO.File.Exists(path), $"{image}: file not found in wwwroot");
        }
    }

    // Tết has every budget with 3 or 4 hampers; 20/11 and the women's page have 300.000đ to 2 triệu with 1 to 3.
    [Theory]
    [InlineData("tet", 10_000_000, 3, 4)]
    [InlineData("20-11", 2_000_000, 1, 3)]
    [InlineData("phu-nu", 2_000_000, 1, 3)]
    [InlineData("valentine", 2_000_000, 1, 3)]
    [InlineData("trung-thu", 2_000_000, 1, 3)]
    [InlineData("giang-sinh", 2_000_000, 1, 3)]
    public void Each_occasion_has_its_budgets_and_hampers_per_budget(string occasion, long highest, int fewest, int most)
    {
        var counts = File.Hampers.Where(h => h.Occasions.Contains(occasion)).GroupBy(h => h.BudgetVnd).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(BudgetTiers.All.Where(t => t <= highest), counts.Keys.Order());
        foreach (var (budget, count) in counts)
        {
            Assert.InRange(count, fewest, most);
        }
    }

    // The last day a hamper of each occasion can be delivered, written out here on purpose (a test that read the occasions
    // would agree with a wrong date). Every item of every hamper must still be in date on that day, with the shelf margin,
    // so a ready hamper is never offered with an item that has gone off by then. 20/11 and Tết are the next ones from
    // 2026-11-20; Trung thu 2027 is the 15th of the 8th lunar month, about 15/09/2027.
    [Theory]
    [InlineData("tet", 2027, 2, 3)]
    [InlineData("valentine", 2027, 2, 12)]
    [InlineData("phu-nu", 2027, 3, 6)]
    [InlineData("trung-thu", 2027, 9, 10)]
    [InlineData("20-11", 2027, 11, 18)]
    [InlineData("giang-sinh", 2026, 12, 22)]
    public void Every_hamper_is_in_date_on_the_last_delivery_day_of_its_occasion(string occasion, int year, int month, int day)
    {
        var delivery = new DateOnly(year, month, day);
        var products = SeedData.Load(SeedData.FilePath).Products.ToDictionary(p => p.Slug);
        var hampers = File.Hampers.Where(h => h.Occasions.Contains(occasion)).ToList();

        Assert.NotEmpty(hampers);
        foreach (var hamper in hampers)
        {
            foreach (var line in hamper.Items)
            {
                var expiry = products[line.Item].ExpiryDate;
                Assert.True(expiry is null || BuilderLimits.IsInDate(expiry.Value, delivery),
                    $"{hamper.Slug}: {line.Item} expires {expiry}, which is too soon for {delivery} plus the shelf margin");
            }
        }
    }

    [Fact]
    public async Task The_database_holds_exactly_the_hampers_from_the_file()
    {
        var rows = await fixture.WithDbAsync(db => db.ReadyHampers.AsNoTracking()
            .Include(h => h.Items).ThenInclude(l => l.Product).Include(h => h.Occasions)
            .OrderBy(h => h.SortOrder).ToListAsync());

        Assert.Equal(File.Hampers.Select(h => h.Slug), rows.Select(r => r.Slug));
        foreach (var (file, row) in File.Hampers.Zip(rows))
        {
            Assert.Equal(file.Items.Select(i => (i.Item, i.Quantity)).Order(), row.Items.Select(l => (l.Product!.Slug, l.Quantity)).Order());
            Assert.Equal(file.Occasions.Order(), row.Occasions.Select(o => o.Id).Order());
            Assert.Equal(file.Packaging, row.PackagingId);
        }

        Assert.Equal(File.Occasions.Select(o => o.Id), await fixture.WithDbAsync(db => db.Occasions.OrderBy(o => o.SortOrder).Select(o => o.Id).ToListAsync()));
    }

    // A total includes its packaging, never passes its budget and is at least about 85 % of it (decision 2026-11-17).
    [Fact]
    public async Task Every_total_with_its_packaging_is_between_85_percent_of_its_budget_and_the_budget()
    {
        foreach (var file in File.Hampers)
        {
            var card = await WithServiceAsync(s => s.FindAsync(file.Slug));
            Assert.NotNull(card);
            Assert.Equal(card.Quote.GoodsVnd + card.Packaging.FeeVnd, card.Quote.TotalVnd);
            Assert.True(card.Quote.TotalVnd <= card.BudgetVnd, $"{file.Slug}: {card.Quote.TotalVnd} over {card.BudgetVnd}");
            Assert.True(card.Quote.TotalVnd * 100 >= card.BudgetVnd * 85, $"{file.Slug}: {card.Quote.TotalVnd} under 85 % of {card.BudgetVnd}");
        }
    }

    // Totals are never stored: a new price shows on the card at once.
    [Fact]
    public async Task A_price_change_shows_in_the_total_at_once()
    {
        var before = (await WithServiceAsync(s => s.FindAsync("an-khang")))!;
        var line = before.Lines.First();
        await SetAsync(line.Slug, price: line.PriceVnd + 1_000);
        try
        {
            var after = (await WithServiceAsync(s => s.FindAsync("an-khang")))!;
            Assert.Equal(before.Quote.TotalVnd + 1_000 * line.Quantity, after.Quote.TotalVnd);
        }
        finally
        {
            await SetAsync(line.Slug, price: line.PriceVnd);
        }
    }

    // A hamper the shop cannot fill stays listed and names the item that is short.
    [Fact]
    public async Task A_hamper_with_an_item_short_lists_that_item_as_sold_out()
    {
        var before = (await WithServiceAsync(s => s.FindAsync("truong-tho")))!;
        Assert.Empty(before.SoldOut);
        var twice = before.Lines.Single(l => l.Quantity == 2);
        await SetAsync(twice.Slug, stock: 1);
        try
        {
            var after = (await WithServiceAsync(s => s.FindAsync("truong-tho")))!;
            Assert.Equal([twice.Slug], after.SoldOut.Select(l => l.Slug));
            var tet = (await WithServiceAsync(s => s.PageAsync("tet")))!;
            Assert.Contains("truong-tho", tet.Groups.SelectMany(g => g.Hampers).Select(c => c.Slug));
        }
        finally
        {
            await SetAsync(twice.Slug, stock: twice.Stock);
        }
    }

    [Fact]
    public async Task An_occasion_page_groups_its_hampers_by_budget_lowest_first_and_shows_only_its_own()
    {
        var page = (await WithServiceAsync(s => s.PageAsync("phu-nu")))!;
        Assert.Equal("8/3 và 20/10", page.Occasion.Name);
        Assert.Equal(page.Groups.Select(g => g.BudgetVnd).Order(), page.Groups.Select(g => g.BudgetVnd));
        var slugs = page.Groups.SelectMany(g => g.Hampers).Select(c => c.Slug).ToList();
        Assert.Equal(File.Hampers.Where(h => h.Occasions.Contains("phu-nu")).Select(h => h.Slug).Order(), slugs.Order());
        // The hamper on two pages is on both.
        Assert.Contains("hoa-giay", slugs);
        Assert.Contains("hoa-giay", (await WithServiceAsync(s => s.PageAsync("20-11")))!.Groups.SelectMany(g => g.Hampers).Select(c => c.Slug));
        Assert.Null(await WithServiceAsync(s => s.PageAsync("khong-co")));
    }

    [Fact]
    public async Task The_earliest_expiry_skips_items_with_none_and_is_null_when_no_item_has_one()
    {
        var card = (await WithServiceAsync(s => s.FindAsync("ban-giay")))!;   // so tay (none), ca phe phin, thiep (none)
        Assert.Equal("ca-phe-phin-giay", card.EarliestExpiry?.Slug);
    }

    [Fact]
    public void A_hamper_of_items_with_no_expiry_date_has_no_earliest_expiry()
    {
        ReadyHamperLine Line(string slug) => new(slug, slug, "", 100, "", null, 1_000, 1, 10);
        var card = new ReadyHamperCard("x", "x", "", 300_000, "", new PackagingOption("kraft", "", "", 0, ""),
            [Line("nen-thom"), Line("thiep")], new Quote(2_000, 0, 2_000, 298_000, false, []), []);
        Assert.Null(card.EarliestExpiry);
    }

    private Task<int> SetAsync(string slug, long? price = null, int? stock = null) => fixture.WithDbAsync(db => db.Products
        .Where(p => p.Slug == slug)
        .ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PriceVnd, p => price ?? p.PriceVnd)
            .SetProperty(p => p.Stock, p => stock ?? p.Stock)));
}
