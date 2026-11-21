using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// What is left of one day, for the checkout (F42): which of its three windows are full, and, when all three are, the
// nearest day that still has room (null when none is found within the search range).
public sealed record DaySlots(DateOnly Date, IReadOnlySet<DeliveryWindow> Full, DateOnly? NearestFreeDay)
{
    public bool DayFull => Full.Count == DeliveryWindows.All.Count;
}

// The setting, read once at start so a wrong value stops the site at once, not at the first checkout.
public sealed record DeliveryLimit(int PerWindow)
{
    public static DeliveryLimit FromConfiguration(IConfiguration configuration) =>
        configuration.GetValue<int?>("Delivery:OrdersPerWindow") is > 0 and var n
            ? new DeliveryLimit(n)
            : throw new InvalidOperationException("Delivery:OrdersPerWindow must be a number above 0 (appsettings.json).");
}

// How many orders one delivery window takes in a day (setting "Delivery:OrdersPerWindow", the owner's number; 30 as
// agreed 2026-11-22). Cancelled orders do not count. Counted from the orders themselves, so there is no second number to
// keep in step. OrderService checks it again inside the transaction that places an order.
public sealed class DeliveryCapacity(PeekboxDbContext db, DeliveryLimit limit, BuilderLimits limits)
{
    // How far from the chosen day the search for a free one looks, each way.
    public const int SearchDays = 30;

    public int PerWindow => limit.PerWindow;

    public IQueryable<Order> Counted => db.Orders.Where(o => o.Status != OrderStatus.Cancelled);

    // The windows of one day that have reached the limit.
    public async Task<DaySlots> SlotsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var from = date.AddDays(-SearchDays);
        var to = date.AddDays(SearchDays);
        var rows = await Counted.AsNoTracking()
            .Where(o => o.DeliveryDate >= from && o.DeliveryDate <= to)
            .GroupBy(o => new { o.DeliveryDate, o.DeliveryWindow })
            .Select(g => new { g.Key.DeliveryDate, g.Key.DeliveryWindow, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var full = FullWindows(rows.Where(r => r.DeliveryDate == date).Select(r => (r.DeliveryWindow, r.Count)));
        if (full.Count < DeliveryWindows.All.Count)
        {
            return new DaySlots(date, full, null);
        }

        // The nearest day with a free window, the later one first when two are as near; only days the builder accepts.
        for (var step = 1; step <= SearchDays; step++)
        {
            foreach (var candidate in new[] { date.AddDays(step), date.AddDays(-step) })
            {
                if (candidate < limits.Earliest || candidate > limits.Latest)
                {
                    continue;
                }

                var taken = rows.Where(r => r.DeliveryDate == candidate).Select(r => (r.DeliveryWindow, r.Count));
                if (FullWindows(taken).Count < DeliveryWindows.All.Count)
                {
                    return new DaySlots(date, full, candidate);
                }
            }
        }

        return new DaySlots(date, full, null);
    }

    private HashSet<DeliveryWindow> FullWindows(IEnumerable<(DeliveryWindow Window, int Count)> counts) =>
        counts.Where(c => c.Count >= PerWindow).Select(c => c.Window).ToHashSet();
}
