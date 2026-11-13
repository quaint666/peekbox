namespace Peekbox.Web.Navigation;

// Simple stroked line icons, each drawn from one path. Add new ones here.
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["cart"] = "M3 4h2l2.4 11h10.2L20 7H6 M9 19h.01 M17 19h.01",
        ["moon"] = "M21 13.5A8.5 8.5 0 1 1 10.5 3a7 7 0 0 0 10.5 10.5z",
        ["sun"] = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M12 2v2 M12 20v2 M4.9 4.9l1.4 1.4 M17.7 17.7l1.4 1.4 M2 12h2 M20 12h2 M4.9 19.1l1.4-1.4 M17.7 6.3l1.4-1.4",
        ["menu"] = "M4 7h16 M4 12h16 M4 17h16",
        ["eye"] = "M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z M9 12a3 3 0 1 0 6 0 3 3 0 1 0-6 0",
        ["wallet"] = "M5 6h14a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M3 10h18 M16.5 14.5h.01",
        ["shield"] = "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6l8-3z M8.5 12l2.5 2.5 4.5-5",
        ["close"] = "M6 6l12 12 M18 6L6 18",
    };

    public static string Path(string name) => Paths[name];
}
