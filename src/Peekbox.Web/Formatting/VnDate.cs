using System.Globalization;

namespace Peekbox.Web.Formatting;

// Dates as Vietnamese readers write them. One place, so every page writes them the same way.
public static class VnDate
{
    // 15/01/2027
    public static string Day(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // 12/2027: the short expiry on an item card that is in date
    public static string Month(DateOnly date) => date.ToString("MM/yyyy", CultureInfo.InvariantCulture);
}
