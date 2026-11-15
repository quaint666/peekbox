using Peekbox.Web.Formatting;

namespace Peekbox.Tests;

public class VndTests
{
    [Theory]
    [InlineData(0, "0đ")]
    [InlineData(45_000, "45.000đ")]
    [InlineData(300_000, "300.000đ")]
    [InlineData(1_000_000, "1.000.000đ")]
    [InlineData(2_715_000, "2.715.000đ")]
    public void Money_is_written_with_dots_and_a_trailing_d(long amount, string expected)
    {
        Assert.Equal(expected, Vnd.Format(amount));
    }
}
