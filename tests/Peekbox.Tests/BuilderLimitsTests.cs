using Peekbox.Web.Services;

namespace Peekbox.Tests;

public class BuilderLimitsTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static BuilderLimits LimitsAt(string utc) => new(new FixedClock(DateTimeOffset.Parse(utc)));

    [Theory]
    [InlineData("500000", 500_000)]
    [InlineData("500.000đ", 500_000)]
    [InlineData("500.000 đ", 500_000)]
    [InlineData("1.000.000Đ", 1_000_000)]
    [InlineData("500 000", 500_000)]
    [InlineData("  300000  ", 300_000)]
    [InlineData("0", 0)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("9.223.372.036.854.775.807", long.MaxValue)]
    [InlineData("1.000 000đ", 1_000_000)]     // a mix of dots and spaces, still in groups of three
    [InlineData("500\u00A0000", 500_000)]     // a non-breaking space, as some keyboards and copy-paste give
    public void A_budget_is_digits_with_optional_dots_spaces_and_a_trailing_d(string text, long expected)
    {
        Assert.Equal(BudgetProblem.None, LimitsAt("2026-11-15T00:00:00Z").ParseBudget(text, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("đ")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("500,000")]
    [InlineData("12abc")]
    [InlineData("٣٠٠")]                      // Arabic-Indic digits are not digits here
    [InlineData("1.5")]                      // a typo: it must not become 15đ
    [InlineData("500.00")]                   // a typo: it must not become 50.000đ
    [InlineData("5.00.000")]                 // groups must have three digits
    [InlineData("1.0000")]
    [InlineData(".500")]
    [InlineData("500.")]
    [InlineData("500..000")]
    [InlineData("1.000.00đ")]
    [InlineData("5 00 000")]
    [InlineData("9223372036854775808")]      // one more than a long can hold: invalid, not an exception
    [InlineData("99999999999999999999999")]
    public void Anything_else_is_an_invalid_budget(string? text)
    {
        Assert.Equal(BudgetProblem.Invalid, LimitsAt("2026-11-15T00:00:00Z").ParseBudget(text, out var amount));
        Assert.Equal(0, amount);
    }

    [Fact]
    public void Today_is_counted_in_Vietnam_time_UTC_plus_7()
    {
        // 16:59:59 UTC is 23:59:59 in Vietnam: still the 15th. One second later it is the 16th.
        Assert.Equal(new DateOnly(2026, 11, 15), LimitsAt("2026-11-15T16:59:59Z").Today);
        Assert.Equal(new DateOnly(2026, 11, 16), LimitsAt("2026-11-15T17:00:00Z").Today);
    }

    [Fact]
    public void The_earliest_date_is_tomorrow_and_the_latest_is_one_year_from_today()
    {
        var limits = LimitsAt("2026-11-15T03:00:00Z");   // 10:00 on the 15th in Vietnam

        Assert.Equal(new DateOnly(2026, 11, 16), limits.Earliest);
        Assert.Equal(new DateOnly(2027, 11, 15), limits.Latest);
    }

    [Theory]
    [InlineData("2026-11-14", DateProblem.TooEarly)]   // yesterday
    [InlineData("2026-11-15", DateProblem.TooEarly)]   // today
    [InlineData("2026-11-16", DateProblem.None)]       // tomorrow: the first allowed day
    [InlineData("2027-11-15", DateProblem.None)]       // exactly one year ahead: the last allowed day
    [InlineData("2027-11-16", DateProblem.TooLate)]
    [InlineData("15/01/2027", DateProblem.Invalid)]    // not the format the date field sends
    [InlineData("2026-13-45", DateProblem.Invalid)]
    [InlineData("tomorrow", DateProblem.Invalid)]
    public void The_date_must_be_between_tomorrow_and_one_year_from_today(string text, DateProblem expected)
    {
        Assert.Equal(expected, LimitsAt("2026-11-15T03:00:00Z").ParseDate(text, out var date));
        Assert.Equal(expected == DateProblem.None ? DateOnly.ParseExact(text, "yyyy-MM-dd") : null, date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void An_empty_date_is_allowed_and_means_no_date_yet(string? text)
    {
        Assert.Equal(DateProblem.None, LimitsAt("2026-11-15T03:00:00Z").ParseDate(text, out var date));
        Assert.Null(date);
    }

    [Fact]
    public void One_year_from_a_leap_day_is_the_28th_of_February()
    {
        var limits = LimitsAt("2028-02-29T03:00:00Z");

        Assert.Equal(new DateOnly(2029, 2, 28), limits.Latest);
        Assert.Equal(DateProblem.None, limits.ParseDate("2029-02-28", out _));
        Assert.Equal(DateProblem.TooLate, limits.ParseDate("2029-03-01", out _));
    }
}
