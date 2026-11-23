using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// What the orders screen asks for (F57). Date null: every day. Status null: every status. Search: free text.
public sealed record AdminOrderFilter(DateOnly? Date, OrderStatus? Status, string? Search);

// One line of the list.
public sealed record AdminOrderRow(int Id, string Number, string RecipientName, int ItemCount, long TotalVnd, PaymentMethod Method, PaymentStatus Payment, OrderStatus Status);

// The list, and how many orders the chosen day holds in all (every status, before the status filter and the search); null
// when no day is chosen. Capped is true when more orders matched than are shown.
public sealed record AdminOrderList(IReadOnlyList<AdminOrderRow> Rows, int? DayCount, bool Capped);

public enum StatusChange
{
    Saved,           // the status moved (or stayed) and the tracking code and the note were saved
    NotFound,        // no such order
    Stale,           // the order is no longer in the status the form was opened with (another person or tab moved it)
    NotAllowed,      // not the next step: a step skipped, backward, cancel (not built yet), or from a final status
    PaymentMissing,  // a ZaloPay order that is not paid cannot go out for delivery
    TooLong,         // the tracking code or the note is longer than the column
}

// The orders of the admin (F57): the list with its filters, one order, and moving an order one step forward. Every write is one
// conditional UPDATE on the status that was read, so two people (or two tabs) moving the same order cannot both win.
public sealed class AdminOrders(PeekboxDbContext db)
{
    // A day cannot hold more than 90 orders today (OrdersPerWindow 30, three windows); the cap is for when that setting is raised.
    public const int ListLimit = 500;

    public const int SearchMaxLength = 60;

    // The one step forward from a status; null for the final ones (Đã giao, Đã huỷ). Cancelling is the next change (F70).
    public static OrderStatus? Next(OrderStatus status) => status switch
    {
        OrderStatus.New => OrderStatus.Preparing,
        OrderStatus.Preparing => OrderStatus.Delivering,
        OrderStatus.Delivering => OrderStatus.Delivered,
        _ => null,
    };

    public async Task<AdminOrderList> ListAsync(AdminOrderFilter filter, CancellationToken cancellationToken = default)
    {
        var orders = db.Orders.AsNoTracking();
        if (filter.Date is { } day)
        {
            orders = orders.Where(o => o.DeliveryDate == day);
        }

        int? dayCount = filter.Date is null ? null : await orders.CountAsync(cancellationToken);

        if (filter.Status is { } status)
        {
            orders = orders.Where(o => o.Status == status);
        }

        if (Clean(filter.Search) is { } text)
        {
            orders = ApplySearch(orders, text);
        }

        var rows = await orders
            .OrderByDescending(o => o.Id)
            .Take(ListLimit + 1)
            .Select(o => new { o.Id, o.RecipientName, Items = o.Items.Sum(i => i.Quantity), o.TotalVnd, o.PaymentMethod, o.PaymentStatus, o.Status })
            .ToListAsync(cancellationToken);

        return new AdminOrderList(
            rows.Take(ListLimit).Select(r => new AdminOrderRow(r.Id, OrderNumber.Format(r.Id), r.RecipientName, r.Items, r.TotalVnd, r.PaymentMethod, r.PaymentStatus, r.Status)).ToList(),
            dayCount,
            rows.Count > ListLimit);
    }

    // The search box: an order number ("PEEK000123", or "123"), a phone (spaces and dots ignored, a part of it is enough) or a
    // name of the sender or the recipient (accents and case ignored). % and _ are plain characters.
    private static IQueryable<Order> ApplySearch(IQueryable<Order> orders, string text)
    {
        var upper = text.ToUpperInvariant();
        var numberMatch = Regex.Match(upper, @"^PEEK0*(\d{1,9})$", RegexOptions.CultureInvariant);
        if (numberMatch.Success)
        {
            var id = int.Parse(numberMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            return orders.Where(o => o.Id == id);
        }

        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        var onlyDigitsAndSeparators = text.All(c => char.IsAsciiDigit(c) || c is ' ' or '.' or '-' or '+');
        var pattern = "%" + text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
        var phonePattern = "%" + digits + "%";
        var idFromDigits = digits.Length is > 0 and <= 9 && onlyDigitsAndSeparators ? int.Parse(digits, CultureInfo.InvariantCulture) : -1;

        return orders.Where(o =>
            o.Id == idFromDigits
            || (digits.Length >= 3 && onlyDigitsAndSeparators && (EF.Functions.Like(o.SenderPhone, phonePattern) || EF.Functions.Like(o.RecipientPhone, phonePattern)))
            || EF.Functions.ILike(EF.Functions.Unaccent(o.RecipientName), EF.Functions.Unaccent(pattern), @"\")
            || EF.Functions.ILike(EF.Functions.Unaccent(o.SenderName), EF.Functions.Unaccent(pattern), @"\"));
    }

    public static string? Clean(string? search)
    {
        var text = (search ?? "").Trim();
        return text.Length == 0 ? null : text.Length > SearchMaxLength ? text[..SearchMaxLength] : text;
    }

    // One order with its lines, or null.
    public Task<Order?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        db.Orders.AsNoTracking().Include(o => o.Items.OrderBy(i => i.Id)).SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    // Saves the form of the detail panel: the status (when it is the next step forward), the tracking code and the note. All or
    // nothing, in one UPDATE that also checks the status the form was opened with. A cash-on-delivery order that is delivered is
    // marked paid in the same UPDATE (the courier took the money); a ZaloPay order that is not paid cannot go out for delivery.
    public async Task<StatusChange> SaveAsync(int id, OrderStatus from, OrderStatus to, string? trackingCode, string? adminNote, CancellationToken cancellationToken = default)
    {
        var tracking = Normalize(trackingCode);
        var note = Normalize(adminNote);
        if ((tracking?.Length ?? 0) > OrderAdminRules.TrackingCodeMaxLength || (note?.Length ?? 0) > OrderAdminRules.AdminNoteMaxLength)
        {
            return StatusChange.TooLong;
        }

        if (to == from)
        {
            // The status stays: only the two texts change, whatever the status is by now.
            var saved = await db.Orders.Where(o => o.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.TrackingCode, tracking).SetProperty(o => o.AdminNote, note), cancellationToken);
            return saved == 1 ? StatusChange.Saved : StatusChange.NotFound;
        }

        if (Next(from) != to)
        {
            return StatusChange.NotAllowed;
        }

        var moved = await db.Orders
            .Where(o => o.Id == id && o.Status == from
                && (to != OrderStatus.Delivering || o.PaymentMethod != PaymentMethod.ZaloPay || o.PaymentStatus == PaymentStatus.Paid))
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, to)
                .SetProperty(o => o.PaymentStatus, o => to == OrderStatus.Delivered && o.PaymentMethod == PaymentMethod.CashOnDelivery && o.PaymentStatus == PaymentStatus.Pending ? PaymentStatus.Paid : o.PaymentStatus)
                .SetProperty(o => o.TrackingCode, tracking)
                .SetProperty(o => o.AdminNote, note), cancellationToken);
        if (moved == 1)
        {
            return StatusChange.Saved;
        }

        // Nothing matched: why? (Read after the fact; the UPDATE above is what decided.)
        var now = await db.Orders.AsNoTracking().Where(o => o.Id == id).Select(o => new { o.Status }).SingleOrDefaultAsync(cancellationToken);
        return now is null ? StatusChange.NotFound : now.Status != from ? StatusChange.Stale : StatusChange.PaymentMissing;
    }

    private static string? Normalize(string? text)
    {
        var trimmed = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
