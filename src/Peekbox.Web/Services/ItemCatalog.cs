using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// A category in the "Danh mục" panel: how many items it has and how many of them are in the box (the "✓ n").
public sealed record CategoryChip(string Id, string Name, int Count, int InBox);

// Quantity is how many are in the hamper; Next says whether one more can go in (Pricing.StatusOf), and OverByVnd is how
// far one more would pass the budget (the "Vượt X" button).
public sealed record ItemCard(
    string Slug, string CategoryId, string Name, string Brand, int WeightGrams, string Origin, DateOnly? ExpiryDate,
    long PriceVnd, string ImagePath, bool InDate, bool SoldOut, int Quantity, ItemStatus Next, long OverByVnd);

// One item as its page shows it (F26). The page checks it against the hamper itself, through the Box.
public sealed record ItemDetail(
    string Slug, string Name, string Brand, string CategoryId, string CategoryName, int WeightGrams, string Origin,
    DateOnly? ExpiryDate, long PriceVnd, int Stock, string Description, string ImagePath)
{
    public bool SoldOut => Stock == 0;

    public PricedItem Priced => new(Slug, PriceVnd, ExpiryDate, Stock);
}

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
// FitBudget: "Vừa ngân sách", only the items that cost no more than what is left of the budget.
public sealed record ItemFilter(string? Category = null, bool OnlyInDate = true, string? Search = null, string? Sort = null, bool FitBudget = false);

// What the item list of /builder shows. Category and Search are null when not used (an unknown category is dropped).
// HiddenCount is how many matching items the "only items in date" checkbox hides (0 when it is off).
public sealed record ItemList(
    IReadOnlyList<CategoryChip> Categories, string? Category, bool OnlyInDate, string? Search, ItemSort Sort,
    IReadOnlyList<ItemCard> Items, int HiddenCount, bool FitBudget = false)
{
    // "Tất cả" with no search and the default order shows one sideways row per category (decision 2026-11-17); anything
    // else is one grid, since a sort across categories only makes sense as one list.
    public bool AsRows => Category is null && Search is null && Sort == ItemSort.Default;

    public int TotalCount => Categories.Sum(c => c.Count);

    public CategoryChip? Chosen => Categories.FirstOrDefault(c => c.Id == Category);
}

// The items of the builder (F16 to F19, F22, F23), checked against the delivery date.
public sealed class ItemCatalog(PeekboxDbContext db)
{
    public const int SearchMaxLength = 100;

    public async Task<ItemList> ListAsync(DateOnly deliveryDate, ItemFilter filter, Box box, CancellationToken cancellationToken = default)
    {
        var boxSlugs = box.Lines.Select(l => l.Slug).ToList();
        var inBox = await db.Products.AsNoTracking()
            .Where(p => boxSlugs.Contains(p.Slug))
            .GroupBy(p => p.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var categories = (await db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.Name, Count = c.Products.Count })
            .ToListAsync(cancellationToken))
            .Select(c => new CategoryChip(c.Id, c.Name, c.Count, inBox.GetValueOrDefault(c.Id)))
            .ToList();

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
            // Items with no expiry date (not food) come last: this sort is for comparing dates.
            ItemSort.ExpiryLatest => products.OrderBy(p => p.ExpiryDate == null).ThenByDescending(p => p.ExpiryDate).ThenBy(p => p.Id),
            ItemSort.NameAz => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            _ => products.OrderBy(p => p.Id),
        };

        var rows = await products.ToListAsync(cancellationToken);

        // The shelf-life rule is checked here in C#, not in SQL, so it is written once (BuilderLimits.IsInDate).
        // Whether one more fits is Pricing's call, through the box (the same rules the add button is checked with).
        var all = rows.Select(p => new ItemCard(
            p.Slug, p.CategoryId, p.Name, p.Brand, p.WeightGrams, p.Origin, p.ExpiryDate, p.PriceVnd, p.ImagePath,
            InDate: BuilderLimits.IsInDate(p.ExpiryDate, deliveryDate),
            SoldOut: p.Stock == 0,
            Quantity: box.QuantityOf(p.Slug),
            Next: box.StatusOf(new PricedItem(p.Slug, p.PriceVnd, p.ExpiryDate, p.Stock)),
            OverByVnd: box.OverByVnd(p.PriceVnd))).ToList();

        var shown = filter.OnlyInDate ? all.Where(i => i.InDate).ToList() : all;
        var hidden = all.Count - shown.Count;
        if (filter.FitBudget)
        {
            // Costing exactly what is left fits, as in Pricing.StatusOf.
            shown = shown.Where(i => i.PriceVnd <= box.Quote.RemainingVnd).ToList();
        }

        return new ItemList(categories, category, filter.OnlyInDate, search, sort, shown, hidden, filter.FitBudget);
    }

    // The item page (F26); null for an unknown slug. Every item has a page, sold out or not.
    public Task<ItemDetail?> FindAsync(string slug, CancellationToken cancellationToken = default) =>
        db.Products.AsNoTracking()
            .Where(p => p.Slug == slug)
            .Select(p => new ItemDetail(
                p.Slug, p.Name, p.Brand, p.CategoryId, p.Category!.Name, p.WeightGrams, p.Origin,
                p.ExpiryDate, p.PriceVnd, p.Stock, p.Description, p.ImagePath))
            .FirstOrDefaultAsync(cancellationToken);
}
