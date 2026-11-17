namespace Peekbox.Web.Data;

// A hamper Peekbox put together for one or more occasions (F15). Its total is never stored: it is worked out from the
// current prices of its items and its packaging, so a price change shows at once. Seeded from content/data/hampers.json.
public class ReadyHamper
{
    public int Id { get; set; }

    // The public name of the hamper, as an item's slug is.
    public required string Slug { get; set; }

    public required string Name { get; set; }

    // Who it is meant for, "Biếu đồng nghiệp".
    public required string ForWhom { get; set; }

    // Whole dong: one of BudgetTiers.All. The total, packaging included, is at most this (a test checks the seed).
    public long BudgetVnd { get; set; }

    public required string PackagingId { get; set; }

    public Packaging? Packaging { get; set; }

    public required string ImagePath { get; set; }

    // The order within its budget on the occasion page: the file's order.
    public int SortOrder { get; set; }

    public List<ReadyHamperItem> Items { get; set; } = [];

    public List<Occasion> Occasions { get; set; } = [];
}
