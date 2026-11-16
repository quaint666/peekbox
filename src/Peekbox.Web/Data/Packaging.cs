namespace Peekbox.Web.Data;

// A way to pack the hamper (F33): kraft box, hard box with a bow, rattan basket. Seeded from content/data/packaging.json.
public class Packaging
{
    // A short readable key such as "kraft" or "hard-bow".
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    // Whole dong, tax included; 0 for the kraft box. Paid on top of the budget, which covers the goods only.
    public long FeeVnd { get; set; }

    public required string ImagePath { get; set; }

    // The order of the choices on the page; the first one is what a hamper gets until the visitor picks one.
    public int SortOrder { get; set; }
}
