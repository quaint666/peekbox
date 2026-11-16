using Peekbox.Web.Services;

namespace Peekbox.Tests;

// The days the tests use, from the app's own rule (BuilderLimits.Today: the date in Vietnam), so a test and the app
// cannot disagree on what "today" is.
internal static class TestDates
{
    public static DateOnly TodayInVietnam => new BuilderLimits(TimeProvider.System).Today;

    // A month ahead: every seed item the tests use is in date for it.
    public static DateOnly Delivery => TodayInVietnam.AddDays(30);
}
