using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Services;

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

    // The occasions and the ready hampers, from content/data/hampers.json (copied as Seed/hampers.json).
    public static string HampersFilePath => Path.Combine(AppContext.BaseDirectory, "Seed", "hampers.json");

    // The delivery wards, from content/data/wards.json (copied as Seed/wards.json).
    public static string WardsFilePath => Path.Combine(AppContext.BaseDirectory, "Seed", "wards.json");

    public static SeedFile Load(string path) =>
        Parse(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    // Throws JsonException if the JSON is broken or a key is missing.
    public static SeedFile? Parse(string json) => JsonSerializer.Deserialize<SeedFile>(json, JsonOptions);

    public static SeedPackagingFile LoadPackaging(string path) =>
        ParsePackaging(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    public static SeedPackagingFile? ParsePackaging(string json) => JsonSerializer.Deserialize<SeedPackagingFile>(json, JsonOptions);

    // The discount codes, from content/data/coupons.json (copied as Seed/coupons.json).
    public static string CouponsFilePath => Path.Combine(AppContext.BaseDirectory, "Seed", "coupons.json");

    public static SeedCouponsFile LoadCoupons(string path) =>
        ParseCoupons(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    public static SeedCouponsFile? ParseCoupons(string json) => JsonSerializer.Deserialize<SeedCouponsFile>(json, JsonOptions);

    public static SeedWardsFile LoadWards(string path) =>
        ParseWards(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    public static SeedWardsFile? ParseWards(string json) => JsonSerializer.Deserialize<SeedWardsFile>(json, JsonOptions);

    public static SeedHampersFile LoadHampers(string path) =>
        ParseHampers(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    public static SeedHampersFile? ParseHampers(string json) => JsonSerializer.Deserialize<SeedHampersFile>(json, JsonOptions);

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

        if (!context.Set<Coupon>().Any())
        {
            // A code the file spells wrongly (an unknown kind, a repeat) stops the start, as a broken file does.
            context.Set<Coupon>().AddRange(LoadCoupons(CouponsFilePath).Coupons.Select(c => new Coupon
            {
                Code = CouponRules.Normalize(c.Code), Kind = Enum.Parse<CouponKind>(c.Kind, ignoreCase: true), Value = c.Value,
                MinGoodsVnd = c.MinGoodsVnd, ValidFrom = c.ValidFrom, ValidTo = c.ValidTo, MaxUses = c.MaxUses,
            }));
        }

        if (!context.Set<Category>().Any())
        {
            context.Set<Category>().AddRange(file.Categories.Select((c, i) => new Category { Id = c.Id, Name = c.NameVi, SortOrder = i }));
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

        if (!context.Set<Occasion>().Any())
        {
            SeedHampers(context, LoadHampers(HampersFilePath));
        }
    }

    // After the items, the packaging and the occasions are saved, since a hamper points at all three. A slug, a
    // packaging or an occasion the file names but the database does not have stops the start, as a broken file does.
    private static void SeedHampers(DbContext context, SeedHampersFile file)
    {
        var occasions = file.Occasions.Select((o, i) => new Occasion
        {
            Id = o.Id, Name = o.Name, Title = o.Title, Chip = o.Chip, Lead = o.Lead, Accent = o.Accent, ImagePath = o.Image, BloomyLink = o.BloomyLink, SortOrder = i,
        }).ToDictionary(o => o.Id);
        context.Set<Occasion>().AddRange(occasions.Values);

        var products = context.Set<Product>().ToDictionary(p => p.Slug, p => p.Id);
        var packaging = context.Set<Packaging>().Select(p => p.Id).ToHashSet();
        context.Set<ReadyHamper>().AddRange(file.Hampers.Select((h, i) => new ReadyHamper
        {
            Slug = h.Slug,
            Name = h.Name,
            ForWhom = h.For,
            BudgetVnd = h.BudgetVnd,
            PackagingId = packaging.Contains(h.Packaging) ? h.Packaging : throw new InvalidOperationException($"{h.Slug}: unknown packaging {h.Packaging}"),
            ImagePath = h.Image,
            SortOrder = i,
            Occasions = h.Occasions.Select(o => occasions.GetValueOrDefault(o) ?? throw new InvalidOperationException($"{h.Slug}: unknown occasion {o}")).ToList(),
            Items = h.Items.Select(item => new ReadyHamperItem
            {
                ProductId = products.TryGetValue(item.Item, out var id) ? id : throw new InvalidOperationException($"{h.Slug}: unknown item {item.Item}"),
                Quantity = item.Quantity,
            }).ToList(),
        }));

        context.SaveChanges();
    }
}

// The shape of content/data/products.json (JSON names are snake_case).
public sealed record SeedFile(List<SeedCategory> Categories, List<SeedProduct> Products);

public sealed record SeedCategory(string Id, string NameVi);

// The shape of content/data/packaging.json.
public sealed record SeedPackagingFile(List<SeedPackaging> Packaging);

public sealed record SeedPackaging(string Id, string NameVi, string DescVi, long FeeVnd, string Image);

// The shape of content/data/hampers.json. Banner is the line of the season banner (F05); Services/SeasonBanner.cs reads it.
public sealed record SeedHampersFile(List<SeedOccasion> Occasions, List<SeedReadyHamper> Hampers);

public sealed record SeedOccasion(string Id, string Name, string Title, string Chip, string Lead, string Accent, string Image, string Banner, string? BloomyLink);

public sealed record SeedReadyHamper(
    string Slug, string Name, string For, long BudgetVnd, string Packaging, List<string> Occasions, string Image, List<SeedReadyHamperItem> Items);

public sealed record SeedReadyHamperItem(string Item, int Quantity);

// The shape of content/data/coupons.json; every key must be there (null for "no limit").
public sealed record SeedCouponsFile(List<SeedCoupon> Coupons);

public sealed record SeedCoupon(string Code, string Kind, long Value, long MinGoodsVnd, DateOnly? ValidFrom, DateOnly? ValidTo, int? MaxUses);

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
    DateOnly? ExpiryDate,   // null for an item that is not food; the key must still be there
    long PriceVnd,
    int Stock,
    string DescriptionVi,
    string Image);
