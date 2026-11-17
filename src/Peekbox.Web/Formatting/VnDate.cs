using System.Globalization;

namespace Peekbox.Web.Formatting;

// Dates as Vietnamese readers write them. One place, so every page writes them the same way.
public static class VnDate
{
    // 15/01/2027
    public static string Day(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // 12/2027: the short expiry on an item card that is in date
    public static string Month(DateOnly date) => date.ToString("MM/yyyy", CultureInfo.InvariantCulture);

    // What an item with no expiry date (not food) shows wherever an expiry date would be.
    public const string NoExpiry = "Không có hạn dùng";

    // "HSD 12/2027", or NoExpiry: the short expiry in a line of item facts.
    public static string Expiry(DateOnly? date) => date is { } d ? "HSD " + Month(d) : NoExpiry;
}
