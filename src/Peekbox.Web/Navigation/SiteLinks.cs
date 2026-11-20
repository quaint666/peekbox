namespace Peekbox.Web.Navigation;

// One link on the site: where it goes and what it says.
public sealed record SiteLink(string Key, string Href, string Label);

// The links of the header and footer, written once.
public static class SiteLinks
{
    // The addresses of the pages built so far. The routes ([HttpGet(SiteLinks.BuilderPath)]) and the links both read
    // these, so a page cannot move without its links moving with it.
    public const string HomePath = "/";
    public const string BuilderPath = "/builder";
    public const string CartPath = "/cart";
    public const string TrackPath = "/track";

    // The health check (Program.cs): not a page, so no link or route test lists it.
    public const string HealthPath = "/healthz";

    // The page shown after an unexpected error. Only the exception handler opens it; a direct visit is a 404.
    public const string ErrorPath = "/error";

    public static readonly SiteLink Home = new("home", HomePath, "Trang chủ");
    public static readonly SiteLink Builder = new("builder", BuilderPath, "Tạo hộp quà");
    public static readonly SiteLink About = new("about", "/about", "Giới thiệu");
    public static readonly SiteLink Commitments = new("commitments", "/about#cam-ket", "Cam kết");
    public static readonly SiteLink Blog = new("blog", "/blog", "Blog");
    public static readonly SiteLink Terms = new("terms", "/legal#dieu-khoan", "Điều khoản");
    public static readonly SiteLink Privacy = new("privacy", "/legal#bao-mat", "Chính sách bảo mật");
    public static readonly SiteLink Cart = new("cart", CartPath, "Giỏ hàng");
    public static readonly SiteLink Track = new("track", TrackPath, "Tra cứu đơn hàng");

    // The page of one item (/items/tra-sen), written once for the cards, the item page and the redirect after adding.
    public const string ItemsPath = "/items";

    public static string Item(string slug) => $"{ItemsPath}/{Uri.EscapeDataString(slug)}";

    // Step 4 of the builder, the review page (F34 to F36).
    public const string ReviewPath = "/builder/review";

    // The builder's forms: "Thêm", − and + and "Bỏ món này" (F20, F35), and step 3, the packaging (F33). The budget
    // card posts to BuilderPath.
    public const string QuantityPath = "/builder/quantity";

    // The amount form's address when it should not go back to the list (the item page, the review page, the cart).
    public static string Quantity(ReturnTo back) => back == ReturnTo.Builder ? QuantityPath : $"{QuantityPath}?from={back.Value()}";

    public const string PackagingPath = "/builder/packaging";

    // The cart's forms: "Thêm vào giỏ hàng" on the review page (F36) and "Xóa hộp" (F37).
    public const string CartAddPath = "/cart/add";

    public const string CartClearPath = "/cart/clear";

    // Checkout (F41 to F48) and the page an item that ran short meanwhile leads to (F50).
    public const string CheckoutPath = "/checkout";

    public const string SoldOutPath = "/checkout/soldout";

    // The confirmation page of one order, opened by its secret token (F51).
    public const string OrdersPath = "/orders";

    public static string Order(string token) => $"{OrdersPath}/{Uri.EscapeDataString(token)}";

    // The occasion pages (F15): /dip/tet, /dip/20-11, /dip/phu-nu. The id is the occasion's key in content/data/hampers.json.
    public const string OccasionsPath = "/dip";

    public static string Occasion(string id) => $"{OccasionsPath}/{Uri.EscapeDataString(id)}";

    // "Dùng hộp này" on an occasion page.
    public const string UseHamperPath = "/dip/dung-hop";

    // Bloomy, the flower shop of the same team: the occasion pages link to it (decision 2026-11-17).
    public const string Bloomy = "https://bloomy.quaintshomelabdomain.uk";

    public static readonly IReadOnlyList<SiteLink> Header = [Home, Builder, About, Commitments, Blog, Track];
    public static readonly IReadOnlyList<SiteLink> FooterExplore = [About, Commitments, Blog, Track];
    public static readonly IReadOnlyList<SiteLink> FooterLegal = [Terms, Privacy];
}
