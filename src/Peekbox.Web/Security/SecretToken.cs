using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Peekbox.Web.Security;

// The long random codes that find a visitor's hamper and open an order's confirmation page. Made here only.
public static class SecretToken
{
    private const int Bytes = 32;   // 256 random bits, 43 characters once encoded; the columns hold 64

    public const int MaxLength = 64;

    public static string New() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(Bytes));

    // Whether a token from a browser is worth a database lookup: present, no longer than the column, and written only
    // with the letters, digits, "-" and "_" that New() makes (a test token is hex, which fits). Anything else cannot
    // name a row, so it is not looked up: a cookie value with a NUL in it would make Postgres refuse the query, and the
    // visitor would get a 500 on every page.
    public static bool IsWellFormed([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? token) =>
        !string.IsNullOrEmpty(token) && token.Length <= MaxLength && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
