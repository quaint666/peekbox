namespace Peekbox.Web.Navigation;

// One step of a page's breadcrumb: a link, or (no Href) the page itself.
public sealed record Crumb(string Label, string? Href = null);

// The trail under the header, declared by each page and drawn by the layout (Views/Shared/_Breadcrumb.cshtml). It always
// starts at "Trang chủ"; the last step is the page itself, so it has no link. The home page and the error pages have none.
public static class Crumbs
{
    public const string ViewDataKey = "Crumbs";

    public static IReadOnlyList<Crumb> Trail(params Crumb[] after) => [new(SiteLinks.Home.Label, SiteLinks.Home.Href), .. after];

    public static Crumb To(SiteLink link) => new(link.Label, link.Href);

    public static Crumb To(string label, string href) => new(label, href);

    public static Crumb Here(string label) => new(label);
}
