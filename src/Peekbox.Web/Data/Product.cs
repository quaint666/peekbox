namespace Peekbox.Web.Data;

public class Product
{
    public int Id { get; set; }

    // The public name of the item in addresses and in the hamper.
    public required string Slug { get; set; }

    public required string CategoryId { get; set; }

    public Category? Category { get; set; }

    public required string Name { get; set; }

    public required string Brand { get; set; }

    public int WeightGrams { get; set; }

    public required string Origin { get; set; }

    public DateOnly ExpiryDate { get; set; }

    // Whole dong, tax included.
    public long PriceVnd { get; set; }

    public int Stock { get; set; }

    public required string Description { get; set; }

    public required string ImagePath { get; set; }
}
