using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Peekbox.Web.Security;

// The long random codes that find a visitor's hamper and open an order's confirmation page. Made here only.
public static class SecretToken
{
    private const int Bytes = 32;   // 256 random bits, 43 characters once encoded; the columns hold 64

    public const int MaxLength = 64;

    public static string New() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(Bytes));
}
