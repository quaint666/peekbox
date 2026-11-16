using Peekbox.Web.Security;

namespace Peekbox.Tests;

// The secret tokens (a hamper's cookie, an order's address). A token from a browser is looked up only when it could be
// one of ours: present and no longer than the column, so a 2,000-character cookie never reaches the database.
public class SecretTokenTests
{
    [Fact]
    public void A_new_token_is_43_url_safe_characters_and_well_formed()
    {
        var token = SecretToken.New();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.True(SecretToken.IsWellFormed(token));
        Assert.NotEqual(token, SecretToken.New());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("a", true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Only_a_present_token_that_fits_the_column_is_looked_up(object? token, bool wellFormed)
    {
        var text = token is int length ? new string('x', length) : (string?)token;

        Assert.Equal(wellFormed, SecretToken.IsWellFormed(text));
    }
}
