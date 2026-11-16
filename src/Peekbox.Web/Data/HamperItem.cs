namespace Peekbox.Web.Data;

// One item in a visitor's hamper and how many. The hamper and the item together are the key, so an item is listed once.
public class HamperItem
{
    public int HamperId { get; set; }

    public Hamper? Hamper { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    // 1 or more; taking the last one out deletes the row.
    public int Quantity { get; set; }
}
