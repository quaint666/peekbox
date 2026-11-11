namespace Peekbox.Web.Data;

public class Category
{
    // A short readable key such as "banh" or "tra-ca-phe".
    public required string Id { get; set; }

    public required string Name { get; set; }

    public List<Product> Products { get; set; } = [];
}
