namespace Peekbox.Web.Data;

// One line of a placed order, copied from the hamper and the catalogue when the order was placed. ProductId and
// Quantity are what a cancellation will give back to stock (M6).
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public required string Name { get; set; }

    public required string Brand { get; set; }

    public required string Origin { get; set; }

    public int WeightGrams { get; set; }

    public DateOnly ExpiryDate { get; set; }

    // Whole dong, tax included, when the order was placed.
    public long UnitPriceVnd { get; set; }

    public int Quantity { get; set; }
}
