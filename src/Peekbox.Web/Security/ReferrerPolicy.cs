namespace Peekbox.Web.Security;

// The one place the referrer policy is written. Program.cs sends it on every response, and the tests compare against it.
// "no-referrer": a link or a request to another site never says which page it came from. The order confirmation
// address holds the order's secret token, so it must not travel to another site in a Referer header.
public static class ReferrerPolicy
{
    public const string HeaderName = "Referrer-Policy";

    public const string Value = "no-referrer";
}
