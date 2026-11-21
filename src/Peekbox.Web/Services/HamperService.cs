using Microsoft.EntityFrameworkCore;
using Npgsql;
using Peekbox.Web.Data;
using Peekbox.Web.Security;

namespace Peekbox.Web.Services;

// Finds and saves a visitor's unfinished hamper. The browser holds only a random token in a cookie.
public sealed class HamperService(PeekboxDbContext db, TimeProvider clock)
{
    // null when there is no cookie, the cookie names no row (an old or made-up token), or the hamper is already an
    // order: an ordered hamper is finished, so the cart is empty and the next save starts a new hamper (F48).
    public async Task<Hamper?> FindAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (!SecretToken.IsWellFormed(token))
        {
            return null;
        }

        return await db.Hampers.FirstOrDefaultAsync(h => h.Token == token && h.OrderId == null, cancellationToken);
    }

    // Updates the visitor's row, or creates it at the first save. A token from the browser is only used to look a row up:
    // an unknown one never becomes the token of a new row.
    public async Task<Hamper> SaveAsync(string? token, long budgetVnd, DateOnly? deliveryDate, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var hamper = await FindAsync(token, cancellationToken);
        if (hamper is null)
        {
            hamper = new Hamper { Token = SecretToken.New(), CreatedAt = now };
            db.Hampers.Add(hamper);
        }

        hamper.BudgetVnd = budgetVnd;
        hamper.DeliveryDate = deliveryDate;
        hamper.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return hamper;
    }

    // The packaging choices, in page order (F33).
    public Task<List<PackagingOption>> PackagingOptionsAsync(CancellationToken cancellationToken = default) =>
        db.Packagings.AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .Select(p => new PackagingOption(p.Id, p.Name, p.Description, p.FeeVnd, p.ImagePath))
            .ToListAsync(cancellationToken);

    // What is in the hamper, in catalogue order, with the money worked out by Pricing. The packaging counts once the box
    // holds an item: the one the visitor picked, or the first choice until they pick (mockups 26 and 27: "chưa chọn"
    // while the box is empty, "Hộp giấy kraft 0đ" once it is not).
    public async Task<Box> BoxAsync(Hamper hamper, CancellationToken cancellationToken = default)
    {
        var rows = await db.HamperItems.AsNoTracking()
            .Where(l => l.HamperId == hamper.Id)
            .OrderBy(l => l.ProductId)
            .Select(l => new
            {
                l.Quantity, l.Product!.Slug, l.Product.Name, l.Product.Brand, l.Product.Origin, l.Product.ImagePath,
                l.Product.WeightGrams, l.Product.ExpiryDate, l.Product.PriceVnd, l.Product.Stock,
            })
            .ToListAsync(cancellationToken);

        var items = rows.ToDictionary(r => r.Slug, r => new PricedItem(r.Slug, r.PriceVnd, r.ExpiryDate, r.Stock));
        var lines = rows.Select(r => new HamperLine(r.Slug, r.Quantity)).ToList();
        var boxLines = rows.Select(r => new BoxLine(
            r.Slug, r.Name, r.Brand, r.Origin, r.ImagePath, r.WeightGrams, r.ExpiryDate, r.PriceVnd, r.Quantity, Pricing.LineVnd(items[r.Slug], r.Quantity),
            InDate: hamper.DeliveryDate is not { } date || BuilderLimits.IsInDate(r.ExpiryDate, date),
            NextStatus: Pricing.StatusOf(items[r.Slug], hamper.BudgetVnd, hamper.DeliveryDate, lines, items))).ToList();

        PackagingOption? packaging = null;
        if (rows.Count > 0)
        {
            var options = await PackagingOptionsAsync(cancellationToken);
            packaging = options.FirstOrDefault(p => p.Id == hamper.PackagingId) ?? options.FirstOrDefault();
        }

        var quote = Pricing.Quote(hamper.BudgetVnd, lines, items, packaging?.FeeVnd ?? 0);
        var startedFrom = hamper.StartedFromId is { } from && rows.Count > 0
            ? await db.ReadyHampers.Where(h => h.Id == from).Select(h => h.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        return new Box(hamper.BudgetVnd, hamper.DeliveryDate, boxLines, quote, lines, items, packaging) { StartedFrom = startedFrom };
    }

    // Saves the visitor's packaging (F33). False for an id that is not a packaging; nothing is saved then.
    public async Task<bool> SetPackagingAsync(Hamper hamper, string packagingId, CancellationToken cancellationToken = default)
    {
        if (!await db.Packagings.AnyAsync(p => p.Id == packagingId, cancellationToken))
        {
            return false;
        }

        hamper.PackagingId = packagingId;
        hamper.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // How many hampers the header's cart link counts (F37): the visitor's hamper is in the cart once it holds an item
    // (decision 2026-11-16), so this is 1 or 0; an ordered hamper is no longer in the cart. One small query, run on every page.
    public async Task<int> CartCountAsync(string? token, CancellationToken cancellationToken = default) =>
        !SecretToken.IsWellFormed(token)
            ? 0
            : await db.HamperItems.AnyAsync(l => l.Hamper!.Token == token && l.Hamper.OrderId == null, cancellationToken) ? 1 : 0;

    // "Xóa hộp" in the cart (F37): the items and the packaging choice go; the budget and the date stay, so the builder
    // opens again at step 2.
    public async Task ClearAsync(Hamper hamper, CancellationToken cancellationToken = default)
    {
        await db.HamperItems.Where(l => l.HamperId == hamper.Id).ExecuteDeleteAsync(cancellationToken);
        hamper.PackagingId = null;
        hamper.StartedFromId = null;
        hamper.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    // "Dùng hộp này" (F15): the ready hamper's items and packaging replace what is in the visitor's box. A budget lower
    // than the hamper's is raised to it; a higher one and the delivery date are kept. A visitor with no hamper yet gets
    // one with the hamper's budget and no date. The rules of SetQuantityAsync are not run here: the caller has checked
    // the stock (ReadyHamperCard.SoldOut), and an item that expires too early for the date is shown and blocked by the
    // builder and the review page, as after a date change (decision 2026-11-17).
    public async Task<Hamper> UseReadyHamperAsync(Hamper? hamper, ReadyHamperCard card, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        if (hamper is null)
        {
            hamper = new Hamper { Token = SecretToken.New(), CreatedAt = now };
            db.Hampers.Add(hamper);
        }
        else
        {
            await db.HamperItems.Where(l => l.HamperId == hamper.Id).ExecuteDeleteAsync(cancellationToken);
        }

        var slugs = card.Lines.Select(l => l.Slug).ToList();
        var ids = await db.Products.Where(p => slugs.Contains(p.Slug)).ToDictionaryAsync(p => p.Slug, p => p.Id, cancellationToken);
        hamper.Items = card.Lines.Select(l => new HamperItem { ProductId = ids[l.Slug], Quantity = l.Quantity }).ToList();
        hamper.PackagingId = card.Packaging.Id;
        hamper.StartedFromId = await db.ReadyHampers.Where(h => h.Slug == card.Slug).Select(h => (int?)h.Id).SingleAsync(cancellationToken);
        hamper.BudgetVnd = Math.Max(hamper.BudgetVnd, card.BudgetVnd);
        hamper.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return hamper;
    }

    // Sets how many of an item are in the hamper (F20). 0 or less takes it out. Going up is allowed only when the last
    // one added still passes every rule of Pricing.StatusOf (stock, the delivery date, the budget); going down always is,
    // so a visitor who lowered the budget can still take things out.
    public async Task<QuantityChange> SetQuantityAsync(Hamper hamper, string slug, int quantity, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ChangeQuantityAsync(hamper, slug, quantity, cancellationToken);
        }
        catch (Exception e) when (e is DbUpdateConcurrencyException || e is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        {
            // Another request changed the same line at the same moment (a double click, two tabs): two adds of a new item
            // both insert its line, and two removals both delete it. The first one is stored now, so look again from the
            // database and apply this request once more; it then updates the line or finds nothing left to do.
            db.ChangeTracker.Clear();
            var fresh = await db.Hampers.FirstAsync(h => h.Id == hamper.Id, cancellationToken);
            return await ChangeQuantityAsync(fresh, slug, quantity, cancellationToken);
        }
    }

    private async Task<QuantityChange> ChangeQuantityAsync(Hamper hamper, string slug, int quantity, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);
        if (product is null)
        {
            return QuantityChange.UnknownItem;
        }

        var line = await db.HamperItems.FirstOrDefaultAsync(l => l.HamperId == hamper.Id && l.ProductId == product.Id, cancellationToken);
        var current = line?.Quantity ?? 0;
        if (quantity == current || (quantity <= 0 && line is null))
        {
            return QuantityChange.Done;
        }

        if (quantity > current)
        {
            // The hamper as it would be one short of the new amount: then "one more" is exactly the last one added.
            var box = await BoxAsync(hamper, cancellationToken);
            var item = new PricedItem(product.Slug, product.PriceVnd, product.ExpiryDate, product.Stock);
            var items = new Dictionary<string, PricedItem>(box.Items) { [slug] = item };
            var lines = box.HamperLines.Where(l => l.Slug != slug).Append(new HamperLine(slug, quantity - 1)).ToList();
            if (Pricing.StatusOf(item, hamper.BudgetVnd, hamper.DeliveryDate, lines, items) != ItemStatus.Ok)
            {
                return QuantityChange.NotAllowed;
            }
        }

        if (quantity <= 0)
        {
            db.HamperItems.Remove(line!);
            // The last item out: the box no longer comes from a ready hamper.
            if (!await db.HamperItems.AnyAsync(l => l.HamperId == hamper.Id && l.ProductId != product.Id, cancellationToken))
            {
                hamper.StartedFromId = null;
            }
        }
        else if (line is null)
        {
            db.HamperItems.Add(new HamperItem { HamperId = hamper.Id, ProductId = product.Id, Quantity = quantity });
        }
        else
        {
            line.Quantity = quantity;
        }

        hamper.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return QuantityChange.Done;
    }
}

public enum QuantityChange { Done, UnknownItem, NotAllowed }

// One line of "Hộp của bạn" and of the review page. InDate is false when the delivery date changed after the item went
// in (the review page blocks it, F35); NextStatus says whether one more fits (it turns the + button off).
public sealed record BoxLine(
    string Slug, string Name, string Brand, string Origin, string ImagePath, int WeightGrams, DateOnly? ExpiryDate,
    long PriceVnd, int Quantity, long LineVnd, bool InDate, ItemStatus NextStatus);

// One packaging choice (F33).
public sealed record PackagingOption(string Id, string Name, string Description, long FeeVnd, string ImagePath);

// The hamper's items, its packaging and its quote. The view shows these numbers and never adds anything up itself.
// Packaging is null while the box is empty (the step is locked then).
public sealed record Box(
    long BudgetVnd, DateOnly? DeliveryDate, IReadOnlyList<BoxLine> Lines, Quote Quote,
    IReadOnlyList<HamperLine> HamperLines, IReadOnlyDictionary<string, PricedItem> Items, PackagingOption? Packaging)
{
    // The name of the ready hamper the box was filled from, while it holds items ("Bạn đang sửa Hộp Sum Vầy").
    public string? StartedFrom { get; init; }

    public int Count => Lines.Sum(l => l.Quantity);

    // Lines whose item expires too early for the delivery date (F35): the review page blocks the order until they go.
    public IReadOnlyList<BoxLine> OutOfDate => Lines.Where(l => !l.InDate).ToList();

    // What is left of the budget, or how far over it the box is (Quote.OverBudget says which).
    public long LeftOrOverVnd => Math.Abs(Quote.RemainingVnd);

    public int QuantityOf(string slug) => Lines.FirstOrDefault(l => l.Slug == slug)?.Quantity ?? 0;

    // Whether one more of an item (in the hamper or not) can go in.
    public ItemStatus StatusOf(PricedItem item) => Pricing.StatusOf(item, BudgetVnd, DeliveryDate, HamperLines, Items);

    // How far one more of an item would pass the budget: the "Vượt X" button, on a card and on the item page.
    public long OverByVnd(long priceVnd) => priceVnd - Quote.RemainingVnd;
}
