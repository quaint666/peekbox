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
        // Arrow on the occasion buttons: a little thicker than the line icons, as in the mockup.
        ["arrow"] = new Custom("0 0 24 24", "2.2", ["M5 12h14M13 6l6 6-6 6"], null),
        // The same arrow the other way: "Hộp trước" on the rows of an occasion page.
        ["arrow-left"] = new Custom("0 0 24 24", "2.2", ["M19 12H5M11 6l-6 6 6 6"], null),
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
        ["search"] = "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M20 20l-3.5-3.5",
        ["lock"] = "M6 11h12v10H6z M8.5 11V7.5a3.5 3.5 0 0 1 7 0V11",
        // The item page (mockup 03): the fact chips, the in-date notice and the three promises.
        ["calendar"] = "M5 5h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z M8 3v4 M16 3v4 M3 10h18",
        ["pin"] = "M12 21s7-6.2 7-12a7 7 0 1 0-14 0c0 5.8 7 12 7 12z M12 6.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z",
        ["weight"] = "M6 8h12l2 12H4L6 8z M12 3a2 2 0 1 0 0 4 2 2 0 0 0 0-4z",
        ["check-circle"] = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z M7.5 12.5l3 3 6-7",
        ["x-circle"] = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z M9 9l6 6 M15 9l-6 6",
        ["alert-circle"] = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z M12 7v6 M12 16.5h.01",
        ["warning"] = "M12 3 2 20h20L12 3z M12 10v4 M12 17.5v.1",
        ["shield"] = "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6l8-3z M8.5 12l2.5 2.5 4.5-5",
        ["truck"] = "M3 6h11v10H3z M14 9h4l3 3v4h-7 M7.5 15.7a1.8 1.8 0 1 0 0 3.6 1.8 1.8 0 0 0 0-3.6z M17.5 15.7a1.8 1.8 0 1 0 0 3.6 1.8 1.8 0 0 0 0-3.6z",
        ["card"] = "M5 6h14a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M3 10h18",
        // The review page's summary sheet on a phone (mockup 29): "Xem tổng kết" and "Thu gọn".
        ["chevron-up"] = "M6 15l6-6 6 6",
        ["chevron-down"] = "M6 9l6 6 6-6",
        // "Danh mục" on the builder: four tiles.
        ["grid"] = "M4 4h6v6H4z M14 4h6v6h-6z M4 14h6v6H4z M14 14h6v6h-6z",
        // A link that leaves the site (Bloomy, on the occasion pages).
        ["external"] = "M14 4h6v6 M20 4l-9 9 M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5",
    };

    public static string Path(string name) => Paths[name];

    public static Custom? CustomShape(string name) => Customs.GetValueOrDefault(name);

    public static bool Exists(string name) => Paths.ContainsKey(name) || Customs.ContainsKey(name);
}
