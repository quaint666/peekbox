using System.Globalization;
using System.Text.RegularExpressions;

namespace Peekbox.Web.Services;

public enum BudgetProblem { None, Invalid }

public enum DateProblem { None, Invalid, TooEarly, TooLate }

// The rules for the budget and the delivery date on /builder, written once (decisions in issue #35).
// "Today" comes from the clock that is passed in, so the tests can pick any day.
public sealed class BuilderLimits(TimeProvider clock)
{
    // Vietnam has no daylight saving, so a fixed offset is enough and needs no time zone database on the server.
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public const string DateFormat = "yyyy-MM-dd";   // what <input type="date"> sends and expects

    public DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(VietnamOffset).DateTime);

    public DateOnly Earliest => Today.AddDays(1);

    public DateOnly Latest => Today.AddYears(1);

    // Items can be chosen once a delivery date is saved (the budget is always saved with it) and that date can still be
    // delivered: tomorrow or later. The builder and the item page both ask this.
    public bool ItemsOpen(DateOnly? deliveryDate) => deliveryDate is { } date && date >= Earliest;

    // The safety margin of F22. An item is in date only when it expires AFTER the delivery date plus this many days:
    // an item that expires on the last day of the margin is out (decision 2026-11-16). A setting once the admin exists (M6).
    public const int ShelfMarginDays = 7;

    public static bool IsInDate(DateOnly expiry, DateOnly deliveryDate) => IsInDate(expiry, deliveryDate, ShelfMarginDays);

    // The same rule with any margin; tests/vectors/pricing.json also checks a margin of 0.
    public static bool IsInDate(DateOnly expiry, DateOnly deliveryDate, int marginDays) => expiry > deliveryDate.AddDays(marginDays);

    // Plain digits (500000), or digits in groups of three split by dots or spaces (500.000, 1.000.000, 500 000), with an
    // optional "đ" at the end. A dot anywhere else is a typo, not a thousands mark: "1.5" and "500.00" used to become 15đ
    // and 50.000đ without a warning, so now they are invalid. [0-9], not \d: \d also matches digits of other scripts.
    private static readonly Regex Grouped = new(@"^[0-9]{1,3}([. \u00A0][0-9]{3})+$", RegexOptions.CultureInvariant);

    // Whole dong, 0 or more, no upper limit except what a long can hold: a number that does not fit is simply invalid.
    public BudgetProblem ParseBudget(string? text, out long amount)
    {
        amount = 0;
        var cleaned = (text ?? "").Trim();
        if (cleaned.EndsWith('đ') || cleaned.EndsWith('Đ'))
        {
            cleaned = cleaned[..^1].TrimEnd();
        }

        var plain = cleaned.Length > 0 && cleaned.All(char.IsAsciiDigit);
        if (!plain && !Grouped.IsMatch(cleaned))
        {
            return BudgetProblem.Invalid;
        }

        cleaned = new string(cleaned.Where(char.IsAsciiDigit).ToArray());

        return long.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out amount)
            ? BudgetProblem.None
            : BudgetProblem.Invalid;
    }

    // An empty date is fine (it is optional until the visitor continues to packaging): date is null and there is no problem.
    public DateProblem ParseDate(string? text, out DateOnly? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return DateProblem.None;
        }

        if (!DateOnly.TryParseExact(text.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return DateProblem.Invalid;
        }

        if (parsed < Earliest)
        {
            return DateProblem.TooEarly;
        }

        if (parsed > Latest)
        {
            return DateProblem.TooLate;
        }

        date = parsed;
        return DateProblem.None;
    }
}
