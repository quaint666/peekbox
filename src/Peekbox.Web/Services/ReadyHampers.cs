using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// Reads the ready hampers and the occasion pages (F15). The money is worked out here from the current prices with
// Pricing, as the visitor's own box is, so a hamper's total always matches what "Dùng hộp này" puts in the box.
public sealed class ReadyHampers(PeekboxDbContext db)
{
    // The occasions in page order, for the links between the pages.
    public Task<List<OccasionLink>> OccasionsAsync(CancellationToken cancellationToken = default) =>
        db.Occasions.AsNoTracking()
            .OrderBy(o => o.SortOrder)
            .Select(o => new OccasionLink(o.Id, o.Name, o.Accent))
            .ToListAsync(cancellationToken);

    // One occasion page: its text and its hampers grouped by budget, lowest first. Null for an unknown occasion.
    public async Task<OccasionPage?> PageAsync(string id, CancellationToken cancellationToken = default)
    {
        var occasion = await db.Occasions.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (occasion is null)
        {
            return null;
        }

        var cards = await CardsAsync(db.ReadyHampers.Where(h => h.Occasions.Any(o => o.Id == id)), cancellationToken);
        var groups = cards.GroupBy(c => c.BudgetVnd).Select(g => new BudgetGroup(g.Key, g.ToList())).ToList();
        return new OccasionPage(occasion, groups);
    }

    // One hamper by its slug, or null.
    public async Task<ReadyHamperCard?> FindAsync(string slug, CancellationToken cancellationToken = default) =>
        (await CardsAsync(db.ReadyHampers.Where(h => h.Slug == slug), cancellationToken)).SingleOrDefault();

    private static async Task<List<ReadyHamperCard>> CardsAsync(IQueryable<ReadyHamper> hampers, CancellationToken cancellationToken)
    {
        var rows = await hampers.AsNoTracking()
            .Include(h => h.Packaging)
            .Include(h => h.Occasions)
            .Include(h => h.Items).ThenInclude(l => l.Product)
            .OrderBy(h => h.BudgetVnd).ThenBy(h => h.SortOrder)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return rows.Select(Card).ToList();
    }

    private static ReadyHamperCard Card(ReadyHamper h)
    {
        var lines = h.Items.OrderBy(l => l.ProductId).Select(l => new ReadyHamperLine(
            l.Product!.Slug, l.Product.Name, l.Product.Brand, l.Product.WeightGrams, l.Product.Origin, l.Product.ExpiryDate,
            l.Product.PriceVnd, l.Quantity, l.Product.Stock)).ToList();
        var items = lines.ToDictionary(l => l.Slug, l => new PricedItem(l.Slug, l.PriceVnd, l.ExpiryDate, l.Stock));
        var packaging = h.Packaging!;
        var quote = Pricing.Quote(h.BudgetVnd, lines.Select(l => new HamperLine(l.Slug, l.Quantity)).ToList(), items, packaging.FeeVnd);
        return new ReadyHamperCard(
            h.Slug, h.Name, h.ForWhom, h.BudgetVnd, h.ImagePath,
            new PackagingOption(packaging.Id, packaging.Name, packaging.Description, packaging.FeeVnd, packaging.ImagePath),
            lines, quote, h.Occasions.OrderBy(o => o.SortOrder).Select(o => o.Id).ToList());
    }
}

// An occasion as the links between the pages and the builder's chips need it.
public sealed record OccasionLink(string Id, string Name, string Accent);

public sealed record OccasionPage(Occasion Occasion, IReadOnlyList<BudgetGroup> Groups);

public sealed record BudgetGroup(long BudgetVnd, IReadOnlyList<ReadyHamperCard> Hampers);

// One item of a ready hamper, with what "Xem từng món" shows.
public sealed record ReadyHamperLine(
    string Slug, string Name, string Brand, int WeightGrams, string Origin, DateOnly? ExpiryDate, long PriceVnd, int Quantity, int Stock);

// A ready hamper with its money worked out. Quote.TotalVnd is the price on the card: the goods and the packaging.
public sealed record ReadyHamperCard(
    string Slug, string Name, string ForWhom, long BudgetVnd, string ImagePath, PackagingOption Packaging,
    IReadOnlyList<ReadyHamperLine> Lines, Quote Quote, IReadOnlyList<string> OccasionIds)
{
    public int Count => Lines.Sum(l => l.Quantity);

    // The item that expires first, for "HSD sớm nhất"; null when no item has an expiry date.
    public ReadyHamperLine? EarliestExpiry => Lines.Where(l => l.ExpiryDate is not null).MinBy(l => l.ExpiryDate);

    // Items the shop has fewer of than the hamper needs: the card stays listed, its button off, with the reason.
    public IReadOnlyList<ReadyHamperLine> SoldOut => Lines.Where(l => l.Stock < l.Quantity).ToList();
}
