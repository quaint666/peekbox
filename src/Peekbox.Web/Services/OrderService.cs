using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Security;

namespace Peekbox.Web.Services;

// What the checkout form sends once its fields have passed the checks (names trimmed, phones as 10 digits).
public sealed record OrderRequest(
    string SenderName, string SenderPhone, string SenderEmail, string RecipientName, string RecipientPhone,
    int WardId, string Street, DeliveryWindow Window, PaymentMethod Payment);

public enum PlaceOutcome
{
    Placed,          // a new order; OrderToken opens it
    AlreadyPlaced,   // this hamper was ordered before (Back, a double click); OrderToken opens that order
    NoHamper,        // no cookie or an unknown one
    NotReady,        // empty box, a delivery date that has passed, or an item out of date: the cart says which
    UnknownWard,
    SoldOut,         // an item no longer has enough stock; the hamper was lowered to what is left (SoldOut lists them)
}

// An item that ran short while the visitor was checking out (F50, state 07). Left is what the hamper now holds.
public sealed record SoldOutLine(string Name, int Wanted, int Left);

public sealed record PlaceResult(PlaceOutcome Outcome, string? OrderToken = null, IReadOnlyList<SoldOutLine>? SoldOut = null);

// Places an order (F48): the one place an order is written. All or nothing, in one transaction: the hamper is priced
// again on the server, every rule is checked again, the stock is taken, the order and its lines are saved with copies
// of everything that can change later, and the hamper is marked as ordered. The browser never sends a price.
public sealed class OrderService(PeekboxDbContext db, HamperService hampers, BuilderLimits limits, TimeProvider clock)
{
    public async Task<PlaceResult> PlaceAsync(string? hamperToken, OrderRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hamperToken) || hamperToken.Length > SecretToken.MaxLength)
        {
            return new PlaceResult(PlaceOutcome.NoHamper);
        }

        // Not HamperService.FindAsync: that one hides an ordered hamper, and an ordered one must lead to its order.
        var hamper = await db.Hampers.FirstOrDefaultAsync(h => h.Token == hamperToken, cancellationToken);
        if (hamper is null)
        {
            return new PlaceResult(PlaceOutcome.NoHamper);
        }

        if (hamper.OrderId is { } orderedId)
        {
            return await AlreadyPlacedAsync(orderedId, cancellationToken);
        }

        var ward = await db.Wards.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.WardId, cancellationToken);
        if (ward is null)
        {
            return new PlaceResult(PlaceOutcome.UnknownWard);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Claim the hamper first. This UPDATE locks its row, so a second request for the same hamper (a double click)
        // waits here until the first one commits, then finds OrderId set, changes nothing and opens the same order.
        var now = clock.GetUtcNow();
        var claimed = await db.Hampers
            .Where(h => h.Id == hamper.Id && h.OrderId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.UpdatedAt, now), cancellationToken);
        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var orderId = await db.Hampers.Where(h => h.Id == hamper.Id).Select(h => h.OrderId).SingleAsync(cancellationToken);
            return await AlreadyPlacedAsync(orderId!.Value, cancellationToken);
        }

        // Priced and checked again from the database, inside the transaction.
        var box = await hampers.BoxAsync(hamper, cancellationToken);
        if (box.Lines.Count == 0 || box.Packaging is null || !limits.ItemsOpen(hamper.DeliveryDate) || box.OutOfDate.Count > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaceResult(PlaceOutcome.NotReady);
        }

        var slugs = box.Lines.Select(l => l.Slug).ToList();
        var productIds = await db.Products.AsNoTracking()
            .Where(p => slugs.Contains(p.Slug))
            .ToDictionaryAsync(p => p.Slug, p => p.Id, cancellationToken);

        // Take the stock. "Stock >= quantity" makes each update all or nothing, so two orders racing for the last ones
        // cannot both win: the loser updates no row. Rows are taken in id order, so two orders cannot deadlock.
        var shortLines = new List<BoxLine>();
        foreach (var line in box.Lines.OrderBy(l => productIds[l.Slug]))
        {
            var id = productIds[line.Slug];
            var taken = await db.Products
                .Where(p => p.Id == id && p.Stock >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - line.Quantity), cancellationToken);
            if (taken == 0)
            {
                shortLines.Add(line);
            }
        }

        if (shortLines.Count > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaceResult(PlaceOutcome.SoldOut, SoldOut: await LowerToStockAsync(hamper, shortLines, cancellationToken));
        }

        var deliveryFee = DeliveryFees.ForRing(ward.Ring);
        var order = new Order
        {
            Token = SecretToken.New(),
            Status = OrderStatus.New,
            PaymentMethod = request.Payment,
            // ZaloPay is a stand-in (decision 2026-11-11): marked paid at once. Cash is paid to the courier.
            PaymentStatus = request.Payment == PaymentMethod.ZaloPay ? PaymentStatus.Paid : PaymentStatus.Pending,
            SenderName = request.SenderName,
            SenderPhone = request.SenderPhone,
            SenderEmail = request.SenderEmail,
            RecipientName = request.RecipientName,
            RecipientPhone = request.RecipientPhone,
            WardId = ward.Id,
            WardName = ward.Name,
            Street = request.Street,
            DeliveryDate = hamper.DeliveryDate!.Value,   // ItemsOpen above means a date is set
            DeliveryWindow = request.Window,
            GoodsVnd = box.Quote.GoodsVnd,
            PackagingName = box.Packaging.Name,
            PackagingFeeVnd = box.Packaging.FeeVnd,
            DeliveryFeeVnd = deliveryFee,
            TotalVnd = box.Quote.TotalVnd + deliveryFee,
            CreatedAt = now,
            Items = box.Lines.Select(l => new OrderItem
            {
                ProductId = productIds[l.Slug], Name = l.Name, Brand = l.Brand, Origin = l.Origin, WeightGrams = l.WeightGrams,
                ExpiryDate = l.ExpiryDate, UnitPriceVnd = l.PriceVnd, Quantity = l.Quantity,
            }).ToList(),
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        hamper.OrderId = order.Id;
        hamper.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlaceResult(PlaceOutcome.Placed, order.Token);
    }

    private async Task<PlaceResult> AlreadyPlacedAsync(int orderId, CancellationToken cancellationToken)
    {
        var token = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Token).SingleAsync(cancellationToken);
        return new PlaceResult(PlaceOutcome.AlreadyPlaced, token);
    }

    // After a failed stock check (the transaction was rolled back): each short line goes down to what is left, or out
    // of the hamper when nothing is left, so the visitor sees the box that can be ordered now.
    private async Task<IReadOnlyList<SoldOutLine>> LowerToStockAsync(Hamper hamper, List<BoxLine> lines, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var fresh = await db.Hampers.SingleAsync(h => h.Id == hamper.Id, cancellationToken);
        var result = new List<SoldOutLine>();
        foreach (var line in lines)
        {
            var left = await db.Products.Where(p => p.Slug == line.Slug).Select(p => p.Stock).SingleAsync(cancellationToken);
            var keep = Math.Min(left, line.Quantity);
            await hampers.SetQuantityAsync(fresh, line.Slug, keep, cancellationToken);
            result.Add(new SoldOutLine(line.Name, line.Quantity, keep));
        }

        return result;
    }
}
