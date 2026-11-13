using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Peekbox.Web.Data;

// Loads the sample catalogue into an empty database. The data lives in
// content/data/products.json (one copy); the project copies it next to the app
// as Seed/products.json. English fields in the file are ignored.
public static class SeedData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        RespectNullableAnnotations = true,
        // Without this a key missing from the file is not an error: text becomes null and numbers become 0.
        RespectRequiredConstructorParameters = true,
    };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Seed", "products.json");

    public static SeedFile Load(string path) =>
        Parse(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    // Throws JsonException if the JSON is broken or a key is missing.
    public static SeedFile? Parse(string json) => JsonSerializer.Deserialize<SeedFile>(json, JsonOptions);

    // Safe to run again: a table that already has rows is left alone.
    public static void Run(DbContext context)
    {
        var file = Load(FilePath);

        if (!context.Set<Category>().Any())
        {
            context.Set<Category>().AddRange(file.Categories.Select(c => new Category { Id = c.Id, Name = c.NameVi }));
        }

        if (!context.Set<Product>().Any())
        {
            context.Set<Product>().AddRange(file.Products.Select(p => new Product
            {
                Slug = p.Slug,
                CategoryId = p.Category,
                Name = p.NameVi,
                Brand = p.Brand,
                WeightGrams = p.WeightG,
                Origin = p.Origin,
                ExpiryDate = p.ExpiryDate,
                PriceVnd = p.PriceVnd,
                Stock = p.Stock,
                Description = p.DescriptionVi,
                ImagePath = p.Image,
            }));
        }

        context.SaveChanges();
    }
}

// The shape of content/data/products.json (JSON names are snake_case).
public sealed record SeedFile(List<SeedCategory> Categories, List<SeedProduct> Products);

public sealed record SeedCategory(string Id, string NameVi);

public sealed record SeedProduct(
    string Slug,
    string Category,
    string NameVi,
    string Brand,
    int WeightG,
    string Origin,
    DateOnly ExpiryDate,
    long PriceVnd,
    int Stock,
    string DescriptionVi,
    string Image);
