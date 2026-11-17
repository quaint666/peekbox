namespace Peekbox.Web.Navigation;

// The ids that addresses jump to (/builder#chon-mon), written once for the views that set them and the code that links
// to them. The scripts in Scripts/ use some of them too; SeamsTests checks that every id a script asks for is on a page.
public static class Anchors
{
    public const string Items = "chon-mon";        // step 2 on /builder: the item list
    public const string Packaging = "dong-goi";    // step 3 on /builder
    public const string ItemBuy = "them";          // the add control on an item's page
    public const string Suggestions = "goi-y";     // "Hộp gợi ý" on /builder
    public const string BudgetCard = "buoc-1";     // step 1 on /builder: the budget and date card

    public static string ItemCard(string slug) => "mon-" + slug;   // one card in the item list

    // On an occasion page: one budget's row, one hamper card, and the hamper's two popovers.
    public static string Budget(long budgetVnd) => "ngan-sach-" + budgetVnd.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string HamperCard(string slug) => "hop-" + slug;

    public static string HamperItems(string slug) => "xem-" + slug;

    public static string HamperReplace(string slug) => "thay-" + slug;

    // "#chon-mon", for an address.
    public static string Link(string id) => "#" + Uri.EscapeDataString(id);
}
