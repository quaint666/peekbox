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
        ["close"] = "M6 6l12 12 M18 6L6 18",
    };

    public static string Path(string name) => Paths[name];
}
