using System.Globalization;

namespace Peekbox.Web.Formatting;

// Money on the site: whole dong, dots between thousands, a trailing "đ" (300.000đ). One place, so every page writes it the same way.
public static class Vnd
{
    public static string Format(long amount) =>
        amount.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.') + "đ";
}
