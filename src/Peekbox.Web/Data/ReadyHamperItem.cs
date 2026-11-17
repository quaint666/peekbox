namespace Peekbox.Web.Data;

// One item of a ready hamper and how many. The hamper and the item together are the key, as in HamperItem.
public class ReadyHamperItem
{
    public int ReadyHamperId { get; set; }

    public ReadyHamper? ReadyHamper { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantity { get; set; }
}
