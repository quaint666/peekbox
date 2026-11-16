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

    // The packaging choices, from content/data/packaging.json (copied as Seed/packaging.json).
    public static string PackagingFilePath => Path.Combine(AppContext.BaseDirectory, "Seed", "packaging.json");

    // The delivery wards, from content/data/wards.json (copied as Seed/wards.json).
    public static string WardsFilePath => Path.Combine(AppContext.BaseDirectory, "Seed", "wards.json");

    public static SeedFile Load(string path) =>
        Parse(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    // Throws JsonException if the JSON is broken or a key is missing.
    public static SeedFile? Parse(string json) => JsonSerializer.Deserialize<SeedFile>(json, JsonOptions);

    public static SeedPackagingFile LoadPackaging(string path) =>
        ParsePackaging(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    public static SeedPackagingFile? ParsePackaging(string json) => JsonSerializer.Deserialize<SeedPackagingFile>(json, JsonOptions);

    public static SeedWardsFile LoadWards(string path) =>
        ParseWards(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    public static SeedWardsFile? ParseWards(string json) => JsonSerializer.Deserialize<SeedWardsFile>(json, JsonOptions);

    // Safe to run again: a table that already has rows is left alone.
    public static void Run(DbContext context)
    {
        var file = Load(FilePath);

        if (!context.Set<Packaging>().Any())
        {
            // The file's order is the page's order; the first choice is the one a hamper gets until the visitor picks.
            context.Set<Packaging>().AddRange(LoadPackaging(PackagingFilePath).Packaging.Select((p, i) => new Packaging
            {
                Id = p.Id, Name = p.NameVi, Description = p.DescVi, FeeVnd = p.FeeVnd, ImagePath = p.Image, SortOrder = i,
            }));
        }

        if (!context.Set<Ward>().Any())
        {
            // The file is already in Vietnamese order; that order is the order of the list on the checkout page.
            context.Set<Ward>().AddRange(LoadWards(WardsFilePath).Wards.Select((w, i) => new Ward { Name = w.Name, Ring = w.Ring, SortOrder = i }));
        }

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

// The shape of content/data/packaging.json.
public sealed record SeedPackagingFile(List<SeedPackaging> Packaging);

public sealed record SeedPackaging(string Id, string NameVi, string DescVi, long FeeVnd, string Image);

// The shape of content/data/wards.json.
public sealed record SeedWardsFile(List<SeedWard> Wards);

public sealed record SeedWard(string Name, int Ring);

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
