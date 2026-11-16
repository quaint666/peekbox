using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

public sealed record CategoryChip(string Id, string Name);

// Quantity is how many are in the hamper; Next says whether one more can go in (Pricing.StatusOf), and OverByVnd is how
// far one more would pass the budget (the "Vượt X" button).
public sealed record ItemCard(
    string Slug, string Name, string Brand, int WeightGrams, string Origin, DateOnly ExpiryDate,
    long PriceVnd, string ImagePath, bool InDate, bool SoldOut, int Quantity, ItemStatus Next, long OverByVnd);

// The sort menu of F19. The view writes the labels; the address uses the short names in ItemSorts.
public enum ItemSort { Default, PriceAsc, PriceDesc, ExpiryLatest, NameAz }

public static class ItemSorts
{
    private static readonly Dictionary<ItemSort, string> Names = new()
    {
        [ItemSort.Default] = "",
        [ItemSort.PriceAsc] = "price-asc",
        [ItemSort.PriceDesc] = "price-desc",
        [ItemSort.ExpiryLatest] = "expiry",
        [ItemSort.NameAz] = "name",
    };

    public static IReadOnlyList<ItemSort> All { get; } = Enum.GetValues<ItemSort>();

    public static string ToQuery(ItemSort sort) => Names[sort];

    // An unknown name (a typo, an old link) is the default order.
    public static ItemSort FromQuery(string? name) => Names.FirstOrDefault(n => n.Value == (name ?? "")).Key;
}

// What the visitor asked for: the address of /builder, as the filter form sends it.
public sealed record ItemFilter(string? Category = null, bool OnlyInDate = true, string? Search = null, string? Sort = null);

// What the item list of /builder shows. Category and Search are null when not used (an unknown category is dropped).
// HiddenCount is how many matching items the "only items in date" checkbox hides (0 when it is off).
public sealed record ItemList(
    IReadOnlyList<CategoryChip> Categories, string? Category, bool OnlyInDate, string? Search, ItemSort Sort,
    IReadOnlyList<ItemCard> Items, int HiddenCount);

// The items of the builder (F16 to F19, F22, F23), checked against the delivery date.
public sealed class ItemCatalog(PeekboxDbContext db)
{
    public const int SearchMaxLength = 100;

    public async Task<ItemList> ListAsync(DateOnly deliveryDate, ItemFilter filter, Box box, CancellationToken cancellationToken = default)
    {
        var categories = await db.Categories.AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CategoryChip(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        // An unknown category in the address (a typo, an old link) shows every item instead of an empty page.
        var category = categories.Any(c => c.Id == filter.Category) ? filter.Category : null;
        // Spaces typed twice or a pasted line break would otherwise match nothing ("bánh  quy").
        var trimmed = filter.Search is null ? null : string.Join(' ', filter.Search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var search = string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > SearchMaxLength ? trimmed[..SearchMaxLength] : trimmed;
        var sort = ItemSorts.FromQuery(filter.Sort);

        var products = db.Products.AsNoTracking();
        if (category is not null)
        {
            products = products.Where(p => p.CategoryId == category);
        }

        if (search is not null)
        {
            // F17: name, brand or origin contains the words, ignoring accents and case. % and _ typed by the visitor are
            // plain characters, not wildcards. SQL: unaccent("Name") ILIKE unaccent(@pattern) ESCAPE '\'.
            var pattern = "%" + search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            products = products.Where(p =>
                EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(pattern), @"\")
                || EF.Functions.ILike(EF.Functions.Unaccent(p.Brand), EF.Functions.Unaccent(pattern), @"\")
                || EF.Functions.ILike(EF.Functions.Unaccent(p.Origin), EF.Functions.Unaccent(pattern), @"\"));
        }

        // Ties keep the catalogue order, so the page never reshuffles between two visits.
        products = sort switch
        {
            ItemSort.PriceAsc => products.OrderBy(p => p.PriceVnd).ThenBy(p => p.Id),
            ItemSort.PriceDesc => products.OrderByDescending(p => p.PriceVnd).ThenBy(p => p.Id),
            ItemSort.ExpiryLatest => products.OrderByDescending(p => p.ExpiryDate).ThenBy(p => p.Id),
            ItemSort.NameAz => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            _ => products.OrderBy(p => p.Id),
        };

        var rows = await products.ToListAsync(cancellationToken);

        // The shelf-life rule is checked here in C#, not in SQL, so it is written once (BuilderLimits.IsInDate).
        // Whether one more fits is Pricing's call, through the box (the same rules the add button is checked with).
        var all = rows.Select(p => new ItemCard(
            p.Slug, p.Name, p.Brand, p.WeightGrams, p.Origin, p.ExpiryDate, p.PriceVnd, p.ImagePath,
            InDate: BuilderLimits.IsInDate(p.ExpiryDate, deliveryDate),
            SoldOut: p.Stock == 0,
            Quantity: box.QuantityOf(p.Slug),
            Next: box.StatusOf(new PricedItem(p.Slug, p.PriceVnd, p.ExpiryDate, p.Stock)),
            OverByVnd: p.PriceVnd - box.Quote.RemainingVnd)).ToList();

        var shown = filter.OnlyInDate ? all.Where(i => i.InDate).ToList() : all;
        return new ItemList(categories, category, filter.OnlyInDate, search, sort, shown, all.Count - shown.Count);
    }
}
