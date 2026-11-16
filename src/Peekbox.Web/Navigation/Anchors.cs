namespace Peekbox.Web.Navigation;

// The ids that addresses jump to (/builder#chon-mon), written once for the views that set them and the code that links
// to them. The scripts in Scripts/ use some of them too; SeamsTests checks that every id a script asks for is on a page.
public static class Anchors
{
    public const string Items = "chon-mon";        // step 2 on /builder: the item list
    public const string Packaging = "dong-goi";    // step 3 on /builder
    public const string ItemBuy = "them";          // the add control on an item's page

    public static string ItemCard(string slug) => "mon-" + slug;   // one card in the item list

    // "#chon-mon", for an address.
    public static string Link(string id) => "#" + Uri.EscapeDataString(id);
}
