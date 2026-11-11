namespace Peekbox.Web.Security;

// The one place the content security policy is written. Program.cs sends it as
// a header on every response, and the tests compare against it.
// It blocks inline scripts, inline styles and anything from other websites.
public static class ContentSecurityPolicy
{
    public const string HeaderName = "Content-Security-Policy";

    public const string Value =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; " +
        "form-action 'self'; frame-ancestors 'none'";
}
