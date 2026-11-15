namespace Peekbox.Web.Navigation;

// Simple stroked line icons, each drawn from one path. Add new ones here.
public static class Icons
{
    // An icon that needs more than one path, or a short text, or its own size (the mockup draws it that way).
    // The view draws the parts one by one, so nothing is written out as raw markup.
    public sealed record Custom(string ViewBox, string StrokeWidth, IReadOnlyList<string> Paths, Label? Text);

    public sealed record Label(string Value, string X, string Y, string FontSize);

    private static readonly Dictionary<string, Custom> Customs = new()
    {
        // Date-stamp ticket with "HSD" and a tick, from the home mockup (Home.dc.html). The text takes its colour
        // from the icon (currentColor), so the dark theme works without extra rules.
        ["stamp"] = new Custom(
            "0 0 30 38",
            "1.7",
            ["M4 3h6a5 3.2 0 0 0 10 0h6v32h-6a5 3.2 0 0 0-10 0H4z", "m10.5 24.5 3.2 3.2 6-6.4"],
            new Label("HSD", "15", "18", "8.6")),
    };

    private static readonly Dictionary<string, string> Paths = new()
    {
        ["cart"] = "M3 4h2l2.4 11h10.2L20 7H6 M9 19h.01 M17 19h.01",
        ["moon"] = "M21 13.5A8.5 8.5 0 1 1 10.5 3a7 7 0 0 0 10.5 10.5z",
        ["sun"] = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M12 2v2 M12 20v2 M4.9 4.9l1.4 1.4 M17.7 17.7l1.4 1.4 M2 12h2 M20 12h2 M4.9 19.1l1.4-1.4 M17.7 6.3l1.4-1.4",
        ["menu"] = "M4 7h16 M4 12h16 M4 17h16",
        ["eye"] = "M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z M9 12a3 3 0 1 0 6 0 3 3 0 1 0-6 0",
        ["wallet"] = "M5 6h14a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M3 10h18 M16.5 14.5h.01",
        ["close"] = "M6 6l12 12 M18 6L6 18",
    };

    public static string Path(string name) => Paths[name];

    public static Custom? CustomShape(string name) => Customs.GetValueOrDefault(name);

    public static bool Exists(string name) => Paths.ContainsKey(name) || Customs.ContainsKey(name);
}
