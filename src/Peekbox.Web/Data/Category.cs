namespace Peekbox.Web.Data;

public class Category
{
    // A short readable key such as "banh" or "tra-ca-phe".
    public required string Id { get; set; }

    public required string Name { get; set; }

    // The order of the category chips on the builder: the order of content/data/products.json.
    public int SortOrder { get; set; }

    public List<Product> Products { get; set; } = [];
}
