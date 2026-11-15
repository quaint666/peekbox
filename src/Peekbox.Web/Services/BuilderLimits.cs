using System.Globalization;

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

    // Accepts digits, with optional dots, spaces and a trailing "đ" (500.000đ, 500 000, 500000). Whole dong, 0 or more,
    // no upper limit except what a long can hold: a number that does not fit is simply invalid.
    public BudgetProblem ParseBudget(string? text, out long amount)
    {
        amount = 0;
        var cleaned = (text ?? "").Trim();
        if (cleaned.EndsWith('đ') || cleaned.EndsWith('Đ'))
        {
            cleaned = cleaned[..^1];
        }

        cleaned = new string(cleaned.Where(c => c != '.' && !char.IsWhiteSpace(c)).ToArray());

        if (cleaned.Length == 0 || !cleaned.All(char.IsAsciiDigit))
        {
            return BudgetProblem.Invalid;
        }

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
