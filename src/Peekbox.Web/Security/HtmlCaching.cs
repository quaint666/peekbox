namespace Peekbox.Web.Security;

// How a page may be cached when nothing more specific was said. Every page shows the visitor's own cart count in the header,
// so a shared cache (a CDN set to "cache everything") must never keep one for someone else: "private" says only the visitor's
// own browser may, and "no-cache" says it must ask the server again before using it. A page that sets its own header (the
// order page, any page with a form: the anti-forgery token makes those "no-store") keeps it; so do static files, which are
// not HTML.
public static class HtmlCaching
{
    public const string Value = "private, no-cache";
}
