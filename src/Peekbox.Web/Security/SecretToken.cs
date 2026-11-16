using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Peekbox.Web.Security;

// The long random codes that find a visitor's hamper and open an order's confirmation page. Made here only.
public static class SecretToken
{
    private const int Bytes = 32;   // 256 random bits, 43 characters once encoded; the columns hold 64

    public const int MaxLength = 64;

    public static string New() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(Bytes));

    // Whether a token from a browser is worth a database lookup: present, and no longer than the column. Anything
    // else cannot name a row, so it is not looked up.
    public static bool IsWellFormed([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? token) =>
        !string.IsNullOrEmpty(token) && token.Length <= MaxLength;
}
