namespace Peekbox.Web.Security;

// The one place "never guess what a file is" is written. Program.cs sends it on every response, and the tests compare
// against it. "nosniff": a browser takes a file for the type the server says, so an upload or an error page can never
// run as a script or a style because it looks like one.
public static class ContentTypeOptions
{
    public const string HeaderName = "X-Content-Type-Options";

    public const string Value = "nosniff";
}
