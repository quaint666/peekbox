using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Security;

namespace Peekbox.Web.Services;

// What the checkout form sends once its fields have passed the checks (names trimmed, phones as 10 digits).
public sealed record OrderRequest(
    string SenderName, string SenderPhone, string SenderEmail, string RecipientName, string RecipientPhone,
    int WardId, string Street, DeliveryWindow Window, PaymentMethod Payment, string? CardMessage = null, bool HidePrices = false);

public enum PlaceOutcome
{
    Placed,          // a new order; OrderToken opens it
    AlreadyPlaced,   // this hamper was ordered before (Back, a double click); OrderToken opens that order
    NoHamper,        // no cookie or an unknown one
    NotReady,        // empty box, a delivery date that has passed, or an item out of date: the cart says which
    UnknownWard,
    WindowFull,      // the chosen window of the delivery date has taken all it can (F42); the checkout says so
    SoldOut,         // an item no longer has enough stock; the hamper was lowered to what is left (SoldOut lists them)
}

// An item that ran short while the visitor was checking out (F50, state 07). Left is what the hamper now holds.
public sealed record SoldOutLine(string Name, int Wanted, int Left);

public sealed record PlaceResult(PlaceOutcome Outcome, string? OrderToken = null, IReadOnlyList<SoldOutLine>? SoldOut = null, bool PaymentFailed = false);

// What a retry or a switch to cash did (F49).
public enum PaymentFix
{
    Paid,           // the retry worked (or cash was chosen): the order is in order
    StillFailed,    // the retry failed again
    NotFailed,      // no such order, or it is not a failed ZaloPay payment: nothing was changed
}

// One ward in the checkout list, with the fee its ring costs.
public sealed record WardOption(int Id, string Name, long FeeVnd);

// Places an order (F48): the one place an order is written. All or nothing, in one transaction: the hamper is priced
// again on the server, every rule is checked again, the stock is taken, the order and its lines are saved with copies
// of everything that can change later, and the hamper is marked as ordered. The browser never sends a price.
public sealed class OrderService(PeekboxDbContext db, HamperService hampers, BuilderLimits limits, DeliveryCapacity capacity, PaymentStandIn payments, TimeProvider clock)
{
    public async Task<PlaceResult> PlaceAsync(string? hamperToken, OrderRequest request, CancellationToken cancellationToken = default)
    {
        if (!SecretToken.IsWellFormed(hamperToken))
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

        // F42: is there room in the chosen window? Two orders for the last place must not both win, so they take turns:
        // an advisory lock on the delivery day (held until this transaction ends) makes the second one count after the
        // first has committed. Another day does not wait.
        var day = hamper.DeliveryDate!.Value;
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SlotLockKey(day)})", cancellationToken);
        var taken = await capacity.Counted.CountAsync(o => o.DeliveryDate == day && o.DeliveryWindow == request.Window, cancellationToken);
        if (taken >= capacity.PerWindow)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaceResult(PlaceOutcome.WindowFull);
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
            var updated = await db.Products
                .Where(p => p.Id == id && p.Stock >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - line.Quantity), cancellationToken);
            if (updated == 0)
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
            PaymentStatus = request.Payment == PaymentMethod.ZaloPay ? (payments.Fails ? PaymentStatus.Failed : PaymentStatus.Paid) : PaymentStatus.Pending,
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
            CardMessage = request.CardMessage,
            HidePrices = request.HidePrices,
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
        return new PlaceResult(PlaceOutcome.Placed, order.Token, PaymentFailed: order.PaymentStatus == PaymentStatus.Failed);
    }

    // F49: the payment of an order failed (ZaloPay), the order and its box are kept. A retry asks the stand-in again; it
    // changes the order only while it is a failed ZaloPay payment of an order that is not cancelled, so a repeated click or
    // a made-up token does nothing.
    public async Task<PaymentFix> RetryPaymentAsync(string? orderToken, CancellationToken cancellationToken = default)
    {
        if (payments.Fails)
        {
            return await IsFailedAsync(orderToken, cancellationToken) ? PaymentFix.StillFailed : PaymentFix.NotFailed;
        }

        return await FailedOrders(orderToken).ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, PaymentStatus.Paid), cancellationToken) == 1
            ? PaymentFix.Paid
            : PaymentFix.NotFailed;
    }

    // F49: "pay in cash on delivery instead": the same order, now cash, not paid yet.
    public async Task<PaymentFix> SwitchToCashAsync(string? orderToken, CancellationToken cancellationToken = default) =>
        await FailedOrders(orderToken).ExecuteUpdateAsync(
            s => s.SetProperty(o => o.PaymentMethod, PaymentMethod.CashOnDelivery).SetProperty(o => o.PaymentStatus, PaymentStatus.Pending), cancellationToken) == 1
            ? PaymentFix.Paid
            : PaymentFix.NotFailed;

    private IQueryable<Order> FailedOrders(string? orderToken) =>
        db.Orders.Where(o => o.Token == orderToken && o.PaymentMethod == PaymentMethod.ZaloPay && o.PaymentStatus == PaymentStatus.Failed && o.Status != OrderStatus.Cancelled);

    private async Task<bool> IsFailedAsync(string? orderToken, CancellationToken cancellationToken) =>
        SecretToken.IsWellFormed(orderToken) && await FailedOrders(orderToken).AnyAsync(cancellationToken);

    // The order this visitor's hamper became, or null (no hamper, or not ordered yet).
    public Task<string?> OrderTokenOfAsync(string? hamperToken, CancellationToken cancellationToken = default) =>
        !SecretToken.IsWellFormed(hamperToken)
            ? Task.FromResult<string?>(null)
            : db.Hampers.Where(h => h.Token == hamperToken && h.Order != null).Select(h => h.Order!.Token).FirstOrDefaultAsync(cancellationToken);

    // An order and its lines by its secret token (the confirmation page), or null. Read-only.
    public Task<Order?> FindAsync(string? orderToken, CancellationToken cancellationToken = default) =>
        !SecretToken.IsWellFormed(orderToken)
            ? Task.FromResult<Order?>(null)
            : db.Orders.AsNoTracking().Include(o => o.Items.OrderBy(i => i.Id)).SingleOrDefaultAsync(o => o.Token == orderToken, cancellationToken);

    // The token of the order with this number and sender's phone (the track page, F60), or null. Both must match, so a
    // wrong number and a wrong phone look the same to the visitor.
    public Task<string?> TokenForAsync(int orderId, string senderPhone, CancellationToken cancellationToken = default) =>
        db.Orders.AsNoTracking().Where(o => o.Id == orderId && o.SenderPhone == senderPhone).Select(o => o.Token).SingleOrDefaultAsync(cancellationToken);

    // The pictures of the products on an order, by product id.
    public async Task<Dictionary<int, string>> ImagesAsync(Order order, CancellationToken cancellationToken = default)
    {
        var ids = order.Items.Select(i => i.ProductId).ToList();
        return await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ImagePath, cancellationToken);
    }

    // The checkout's ward list, in page order (the file's Vietnamese order).
    public async Task<List<WardOption>> WardsAsync(CancellationToken cancellationToken = default) =>
        (await db.Wards.AsNoTracking().OrderBy(w => w.SortOrder).Select(w => new { w.Id, w.Name, w.Ring }).ToListAsync(cancellationToken))
            .Select(w => new WardOption(w.Id, w.Name, DeliveryFees.ForRing(w.Ring)))
            .ToList();

    // One number per delivery day for pg_advisory_xact_lock; the prefix keeps it clear of any other lock the site might take.
    private static long SlotLockKey(DateOnly day) => 0x50_45_45_4B_00_00_00_00L + day.DayNumber;

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
