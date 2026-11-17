using System.Globalization;

namespace Peekbox.Web.Formatting;

// Money on the site: whole dong, dots between thousands, a trailing "đ" (300.000đ). One place, so every page writes it the same way.
public static class Vnd
{
    public static string Format(long amount) =>
        amount.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.') + "đ";

    // A budget on a chip: "1 triệu" from one million up, when it is whole millions; Format otherwise ("300.000đ").
    public static string Short(long amount) =>
        amount >= 1_000_000 && amount % 1_000_000 == 0 ? $"{amount / 1_000_000} triệu" : Format(amount);
}
