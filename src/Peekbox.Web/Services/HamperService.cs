using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// Finds and saves a visitor's unfinished hamper. The browser holds only a random token in a cookie.
public sealed class HamperService(PeekboxDbContext db, TimeProvider clock)
{
    public const string CookieName = "peekbox_hamper";

    public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(180);

    private const int TokenBytes = 32;   // 43 characters once encoded; the column holds 64

    // null when there is no cookie, or the cookie names no row (an old or made-up token).
    public async Task<Hamper?> FindAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 64)
        {
            return null;
        }

        return await db.Hampers.FirstOrDefaultAsync(h => h.Token == token, cancellationToken);
    }

    // Updates the visitor's row, or creates it at the first save. A token from the browser is only used to look a row up:
    // an unknown one never becomes the token of a new row.
    public async Task<Hamper> SaveAsync(string? token, long budgetVnd, DateOnly? deliveryDate, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var hamper = await FindAsync(token, cancellationToken);
        if (hamper is null)
        {
            hamper = new Hamper { Token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes)), CreatedAt = now };
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
        return new Box(hamper.BudgetVnd, hamper.DeliveryDate, boxLines, quote, lines, items, packaging);
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

    // Sets how many of an item are in the hamper (F20). 0 or less takes it out. Going up is allowed only when the last
    // one added still passes every rule of Pricing.StatusOf (stock, the delivery date, the budget); going down always is,
    // so a visitor who lowered the budget can still take things out.
    public async Task<QuantityChange> SetQuantityAsync(Hamper hamper, string slug, int quantity, CancellationToken cancellationToken = default)
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
    string Slug, string Name, string Brand, string Origin, string ImagePath, int WeightGrams, DateOnly ExpiryDate,
    long PriceVnd, int Quantity, long LineVnd, bool InDate, ItemStatus NextStatus);

// One packaging choice (F33).
public sealed record PackagingOption(string Id, string Name, string Description, long FeeVnd, string ImagePath);

// The hamper's items, its packaging and its quote. The view shows these numbers and never adds anything up itself.
// Packaging is null while the box is empty (the step is locked then).
public sealed record Box(
    long BudgetVnd, DateOnly? DeliveryDate, IReadOnlyList<BoxLine> Lines, Quote Quote,
    IReadOnlyList<HamperLine> HamperLines, IReadOnlyDictionary<string, PricedItem> Items, PackagingOption? Packaging)
{
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
