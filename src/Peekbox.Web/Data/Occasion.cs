namespace Peekbox.Web.Data;

// One occasion page (F15): Tết, 20/11, or 8/3 and 20/10 together. Seeded from content/data/hampers.json.
public class Occasion
{
    // A short readable key, also the page's address: "tet" is /dip/tet.
    public required string Id { get; set; }

    // "Tết", "20/11", "8/3 và 20/10": the chips and the links to the other occasions.
    public required string Name { get; set; }

    // The page's heading, "Hộp quà Tết".
    public required string Title { get; set; }

    // The small label over the heading, "Mùa Tết 2027".
    public required string Chip { get; set; }

    public required string Lead { get; set; }

    // "tet", "teachers" or "women": the page maps it to a CSS class (the CSP forbids inline colours; decision 2026-11-13).
    public required string Accent { get; set; }

    // The hero photo, as on the home page's card.
    public required string ImagePath { get; set; }

    // The text of the link to Bloomy's flowers; null for an occasion Bloomy has no flowers for (Tết, decision 2026-11-17).
    public string? BloomyLink { get; set; }

    // The order of the occasions on the home page and in the links between them.
    public int SortOrder { get; set; }

    public List<ReadyHamper> Hampers { get; set; } = [];
}
