namespace Peekbox.Web.Navigation;

// One link on the site: where it goes and what it says.
public sealed record SiteLink(string Key, string Href, string Label);

// The links of the header and footer, written once.
public static class SiteLinks
{
    public static readonly SiteLink Home = new("home", "/", "Trang chủ");
    public static readonly SiteLink Builder = new("builder", "/builder", "Tạo hộp quà");
    public static readonly SiteLink About = new("about", "/about", "Giới thiệu");
    public static readonly SiteLink Commitments = new("commitments", "/about#cam-ket", "Cam kết");
    public static readonly SiteLink Blog = new("blog", "/blog", "Blog");
    public static readonly SiteLink Terms = new("terms", "/legal#dieu-khoan", "Điều khoản");
    public static readonly SiteLink Privacy = new("privacy", "/legal#bao-mat", "Chính sách bảo mật");
    public static readonly SiteLink Cart = new("cart", "/cart", "Giỏ hàng");

    // The page of one item (/items/tra-sen), written once for the cards, the item page and the redirect after adding.
    public const string ItemsPath = "/items";

    public static string Item(string slug) => $"{ItemsPath}/{Uri.EscapeDataString(slug)}";

    // Step 4 of the builder, the review page (F34 to F36).
    public const string ReviewPath = "/builder/review";

    // Checkout (F41 to F48) and the page an item that ran short meanwhile leads to (F50).
    public const string CheckoutPath = "/checkout";

    public const string SoldOutPath = "/checkout/soldout";

    // The confirmation page of one order, opened by its secret token (F51).
    public const string OrdersPath = "/orders";

    public static string Order(string token) => $"{OrdersPath}/{Uri.EscapeDataString(token)}";

    public static readonly IReadOnlyList<SiteLink> Header = [Home, Builder, About, Commitments, Blog];
    public static readonly IReadOnlyList<SiteLink> FooterExplore = [About, Commitments, Blog];
    public static readonly IReadOnlyList<SiteLink> FooterLegal = [Terms, Privacy];
}
