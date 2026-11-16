namespace Peekbox.Web.Services;

// What can stop one more of an item going into the hamper, in the order they are checked (a sold-out item that is also
// out of date is reported as sold out).
public enum ItemStatus { Ok, SoldOut, ExpiredForDate, OverBudget }

// The facts about an item that the money and expiry rules need.
public sealed record PricedItem(string Slug, long PriceVnd, DateOnly ExpiryDate, int Stock);

// One line of the hamper: an item and how many.
public sealed record HamperLine(string Slug, int Quantity);

// RemainingVnd goes negative when the hamper is over budget. MissingSlugs are lines whose item no longer exists; they
// are left out of the total.
public sealed record Quote(long GoodsVnd, long PackagingVnd, long TotalVnd, long RemainingVnd, bool OverBudget, IReadOnlyList<string> MissingSlugs);

// The money rules of the builder and the review page (F20, F21, F23, F36), written once. Plain functions with no database, so tests/vectors/pricing.json can run
// against them directly. The budget covers the goods only; packaging is paid on top of it (decision 2026-11-11).
public static class Pricing
{
    public static Quote Quote(long budgetVnd, IReadOnlyList<HamperLine> lines, IReadOnlyDictionary<string, PricedItem> items, long packagingVnd)
    {
        long goods = 0;
        var missing = new List<string>();
        foreach (var line in lines)
        {
            if (items.TryGetValue(line.Slug, out var item))
            {
                goods += item.PriceVnd * line.Quantity;
            }
            else
            {
                missing.Add(line.Slug);
            }
        }

        var remaining = budgetVnd - goods;
        return new Quote(goods, packagingVnd, goods + packagingVnd, remaining, remaining < 0, missing);
    }

    // Whether one more of this item can go into the hamper. The expiry rule waits until a delivery date is chosen.
    public static ItemStatus StatusOf(
        PricedItem item, long budgetVnd, DateOnly? deliveryDate, IReadOnlyList<HamperLine> lines, IReadOnlyDictionary<string, PricedItem> items)
    {
        var inHamper = lines.Where(l => l.Slug == item.Slug).Sum(l => l.Quantity);
        if (item.Stock <= inHamper)
        {
            return ItemStatus.SoldOut;
        }

        if (deliveryDate is { } date && !BuilderLimits.IsInDate(item.ExpiryDate, date))
        {
            return ItemStatus.ExpiredForDate;
        }

        // Costing exactly what is left is fine.
        return item.PriceVnd > Quote(budgetVnd, lines, items, 0).RemainingVnd ? ItemStatus.OverBudget : ItemStatus.Ok;
    }
}
