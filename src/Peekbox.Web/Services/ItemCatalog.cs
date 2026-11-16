using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

public sealed record CategoryChip(string Id, string Name);

public sealed record ItemCard(
    string Slug, string Name, string Brand, int WeightGrams, string Origin, DateOnly ExpiryDate,
    long PriceVnd, string ImagePath, bool InDate, bool SoldOut);

// What the item list of /builder shows. Category is null for "Tất cả". HiddenCount is how many items of that category the
// "only items in date" checkbox hides (0 when it is off).
public sealed record ItemList(
    IReadOnlyList<CategoryChip> Categories, string? Category, bool OnlyInDate,
    IReadOnlyList<ItemCard> Items, int HiddenCount);

// The items of the builder (F16, F18, F22, F23), checked against the delivery date.
public sealed class ItemCatalog(PeekboxDbContext db)
{
    public async Task<ItemList> ListAsync(DateOnly deliveryDate, string? categoryId, bool onlyInDate, CancellationToken cancellationToken = default)
    {
        var categories = await db.Categories.AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CategoryChip(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        // An unknown category in the address (a typo, an old link) shows every item instead of an empty page.
        var category = categories.Any(c => c.Id == categoryId) ? categoryId : null;

        var products = db.Products.AsNoTracking();
        if (category is not null)
        {
            products = products.Where(p => p.CategoryId == category);
        }

        var rows = await products.OrderBy(p => p.Id).ToListAsync(cancellationToken);

        // The shelf-life rule is checked here in C#, not in SQL, so it is written once (BuilderLimits.IsInDate).
        var all = rows.Select(p => new ItemCard(
            p.Slug, p.Name, p.Brand, p.WeightGrams, p.Origin, p.ExpiryDate, p.PriceVnd, p.ImagePath,
            InDate: BuilderLimits.IsInDate(p.ExpiryDate, deliveryDate),
            SoldOut: p.Stock == 0)).ToList();

        var shown = onlyInDate ? all.Where(i => i.InDate).ToList() : all;
        return new ItemList(categories, category, onlyInDate, shown, all.Count - shown.Count);
    }
}
