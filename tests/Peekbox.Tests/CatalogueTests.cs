using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Tests;

// The sample catalogue in content/data/products.json, checked against itself,
// against the image files, and against what ends up in the database.
[Collection(DatabaseCollection.Name)]
public class CatalogueTests(PeekboxFixture fixture)
{
    private static readonly SeedFile File = SeedData.Load(SeedData.FilePath);

    [Fact]
    public void Product_slugs_are_unique_and_every_product_has_a_name_and_description()
    {
        Assert.Equal(File.Products.Count, File.Products.Select(p => p.Slug).Distinct().Count());
        foreach (var p in File.Products)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.NameVi), $"{p.Slug}: missing a name");
            Assert.False(string.IsNullOrWhiteSpace(p.DescriptionVi), $"{p.Slug}: missing a description");
        }
    }

    [Fact]
    public void Every_product_has_a_known_category_a_whole_dong_price_and_a_stock()
    {
        var categories = File.Categories.Select(c => c.Id).ToHashSet();
        foreach (var p in File.Products)
        {
            Assert.True(categories.Contains(p.Category), $"{p.Slug}: unknown category {p.Category}");
            Assert.True(p.PriceVnd > 0, $"{p.Slug}: bad price");
            Assert.True(p.Stock >= 0, $"{p.Slug}: negative stock");
        }
    }

    [Fact]
    public void Every_image_path_points_at_a_file_in_wwwroot()
    {
        foreach (var p in File.Products)
        {
            Assert.StartsWith("/images/", p.Image);
            var path = Path.Combine(RepoRoot.WebProject, "wwwroot", p.Image.TrimStart('/'));
            Assert.True(System.IO.File.Exists(path), $"{p.Image}: file not found in wwwroot");
        }
    }

    [Fact]
    public async Task The_database_holds_exactly_the_catalogue_from_the_file()
    {
        var (products, categories) = await fixture.WithDbAsync(async db =>
            (await db.Products.CountAsync(), await db.Categories.CountAsync()));

        Assert.Equal(File.Products.Count, products);
        Assert.Equal(File.Categories.Count, categories);
    }

    [Fact]
    public async Task Running_the_seed_again_adds_nothing()
    {
        var count = await fixture.WithDbAsync(async db =>
        {
            SeedData.Run(db);
            return await db.Products.CountAsync();
        });

        Assert.Equal(File.Products.Count, count);
    }

    [Fact]
    public async Task The_database_refuses_a_free_or_negative_priced_item_and_a_duplicate_slug()
    {
        await fixture.WithDbAsync(async db =>
        {
            var first = await db.Products.FirstAsync();
            db.Products.Add(new Product
            {
                Slug = first.Slug, CategoryId = first.CategoryId, Name = "x", Brand = "x", Origin = "x",
                Description = "x", ImagePath = "/images/x.webp", PriceVnd = 1, Stock = 1, WeightGrams = 1,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            db.Products.Add(new Product
            {
                Slug = "free-item", CategoryId = first.CategoryId, Name = "x", Brand = "x", Origin = "x",
                Description = "x", ImagePath = "/images/x.webp", PriceVnd = 0, Stock = 1, WeightGrams = 1,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            return 0;
        });
    }
}
