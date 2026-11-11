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

    public static readonly IReadOnlyList<SiteLink> Header = [Home, Builder, About, Commitments, Blog];
    public static readonly IReadOnlyList<SiteLink> Footer = [About, Commitments, Blog, Terms, Privacy];
}
