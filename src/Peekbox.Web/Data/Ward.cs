namespace Peekbox.Web.Data;

// A Ho Chi Minh City ward the shop delivers to (F41). Seeded from content/data/wards.json; the ring decides the fee
// (Services/DeliveryFees.cs). An order links to its ward and also keeps a copy of the name and the fee it paid.
public class Ward
{
    public int Id { get; set; }

    public required string Name { get; set; }

    // 1, 2 or 3: how far from the shop.
    public int Ring { get; set; }

    // The order of the list on the checkout page: the file's order, which is Vietnamese alphabetical order.
    public int SortOrder { get; set; }
}
