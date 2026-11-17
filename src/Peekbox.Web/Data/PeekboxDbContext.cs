using Microsoft.EntityFrameworkCore;

namespace Peekbox.Web.Data;

public class PeekboxDbContext(DbContextOptions<PeekboxDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Hamper> Hampers => Set<Hamper>();

    public DbSet<HamperItem> HamperItems => Set<HamperItem>();

    public DbSet<Packaging> Packagings => Set<Packaging>();

    public DbSet<Ward> Wards => Set<Ward>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Occasion> Occasions => Set<Occasion>();

    public DbSet<ReadyHamper> ReadyHampers => Set<ReadyHamper>();

    public DbSet<ReadyHamperItem> ReadyHamperItems => Set<ReadyHamperItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Accent-free search on the builder ("mut" finds "Mứt", "dak lak" finds "Đắk Lắk"). The extension comes with
        // PostgreSQL (on Fedora in the postgresql-contrib package) and a database owner may switch it on.
        modelBuilder.HasPostgresExtension("unaccent");

        modelBuilder.Entity<Category>(category =>
        {
            category.Property(c => c.Id).HasMaxLength(40);
            category.Property(c => c.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.HasIndex(p => p.Slug).IsUnique();
            product.Property(p => p.Slug).HasMaxLength(100);
            product.Property(p => p.CategoryId).HasMaxLength(40);
            product.Property(p => p.Name).HasMaxLength(200);
            product.Property(p => p.Brand).HasMaxLength(100);
            product.Property(p => p.Origin).HasMaxLength(100);
            product.Property(p => p.Description).HasMaxLength(1000);
            product.Property(p => p.ImagePath).HasMaxLength(300);
            // A category with items cannot be deleted by accident.
            product.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
            product.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Products_PriceVnd", "\"PriceVnd\" > 0");
                table.HasCheckConstraint("CK_Products_Stock", "\"Stock\" >= 0");
            });
        });

        modelBuilder.Entity<Hamper>(hamper =>
        {
            hamper.HasIndex(h => h.Token).IsUnique();
            hamper.Property(h => h.Token).HasMaxLength(64);
            hamper.Property(h => h.PackagingId).HasMaxLength(40);
            // A packaging that some hamper uses cannot be deleted by accident.
            hamper.HasOne(h => h.Packaging).WithMany().HasForeignKey(h => h.PackagingId).OnDelete(DeleteBehavior.Restrict);
            hamper.ToTable(table => table.HasCheckConstraint("CK_Hampers_BudgetVnd", "\"BudgetVnd\" >= 0"));
            // One hamper makes at most one order; an order is never deleted while a hamper points at it.
            hamper.HasIndex(h => h.OrderId).IsUnique();
            hamper.HasOne(h => h.Order).WithOne().HasForeignKey<Hamper>(h => h.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Packaging>(packaging =>
        {
            packaging.Property(p => p.Id).HasMaxLength(40);
            packaging.Property(p => p.Name).HasMaxLength(100);
            packaging.Property(p => p.Description).HasMaxLength(300);
            packaging.Property(p => p.ImagePath).HasMaxLength(300);
            packaging.ToTable(table => table.HasCheckConstraint("CK_Packagings_FeeVnd", "\"FeeVnd\" >= 0"));
        });

        modelBuilder.Entity<Ward>(ward =>
        {
            ward.HasIndex(w => w.Name).IsUnique();
            ward.Property(w => w.Name).HasMaxLength(100);
            ward.ToTable(table => table.HasCheckConstraint("CK_Wards_Ring", "\"Ring\" BETWEEN 1 AND 3"));
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.HasIndex(o => o.Token).IsUnique();
            order.Property(o => o.Token).HasMaxLength(64);
            // Enums as text, not EF's default number, so the rows read plainly in SQL and in the admin later.
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            order.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            order.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            order.Property(o => o.DeliveryWindow).HasConversion<string>().HasMaxLength(20);
            order.Property(o => o.SenderName).HasMaxLength(100);
            order.Property(o => o.SenderPhone).HasMaxLength(10);
            order.Property(o => o.SenderEmail).HasMaxLength(254);
            order.Property(o => o.RecipientName).HasMaxLength(100);
            order.Property(o => o.RecipientPhone).HasMaxLength(10);
            order.Property(o => o.WardName).HasMaxLength(100);
            order.Property(o => o.Street).HasMaxLength(300);
            order.Property(o => o.PackagingName).HasMaxLength(100);
            // The admin's day view (M6) lists orders by delivery date; the track page finds one by id and phone.
            order.HasIndex(o => o.DeliveryDate);
            order.HasOne(o => o.Ward).WithMany().HasForeignKey(o => o.WardId).OnDelete(DeleteBehavior.Restrict);
            order.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Orders_Money",
                    "\"GoodsVnd\" >= 0 AND \"PackagingFeeVnd\" >= 0 AND \"DeliveryFeeVnd\" >= 0 AND " +
                    "\"TotalVnd\" = \"GoodsVnd\" + \"PackagingFeeVnd\" + \"DeliveryFeeVnd\"");
                table.HasCheckConstraint("CK_Orders_Phones", "\"SenderPhone\" ~ '^0[0-9]{9}$' AND \"RecipientPhone\" ~ '^0[0-9]{9}$'");
            });
        });

        modelBuilder.Entity<OrderItem>(line =>
        {
            line.Property(l => l.Name).HasMaxLength(200);
            line.Property(l => l.Brand).HasMaxLength(100);
            line.Property(l => l.Origin).HasMaxLength(100);
            // Deleting an order deletes its lines; an item that was ever ordered cannot be deleted by accident.
            line.HasOne(l => l.Order).WithMany(o => o.Items).HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
            line.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            line.ToTable(table =>
            {
                table.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
                table.HasCheckConstraint("CK_OrderItems_UnitPriceVnd", "\"UnitPriceVnd\" >= 0");
            });
        });

        modelBuilder.Entity<Occasion>(occasion =>
        {
            occasion.Property(o => o.Id).HasMaxLength(40);
            occasion.Property(o => o.Name).HasMaxLength(100);
            occasion.Property(o => o.Title).HasMaxLength(200);
            occasion.Property(o => o.Chip).HasMaxLength(100);
            occasion.Property(o => o.Lead).HasMaxLength(1000);
            occasion.Property(o => o.Accent).HasMaxLength(20);
            occasion.Property(o => o.BloomyLink).HasMaxLength(200);
            occasion.Property(o => o.ImagePath).HasMaxLength(300);
        });

        modelBuilder.Entity<ReadyHamper>(hamper =>
        {
            hamper.HasIndex(h => h.Slug).IsUnique();
            hamper.Property(h => h.Slug).HasMaxLength(100);
            hamper.Property(h => h.Name).HasMaxLength(200);
            hamper.Property(h => h.ForWhom).HasMaxLength(100);
            hamper.Property(h => h.PackagingId).HasMaxLength(40);
            hamper.Property(h => h.ImagePath).HasMaxLength(300);
            hamper.HasOne(h => h.Packaging).WithMany().HasForeignKey(h => h.PackagingId).OnDelete(DeleteBehavior.Restrict);
            // A hamper can be on several occasion pages (one pool, decision 2026-11-17): a join table with the two keys.
            hamper.HasMany(h => h.Occasions).WithMany(o => o.Hampers).UsingEntity("ReadyHamperOccasions");
            hamper.ToTable(table => table.HasCheckConstraint("CK_ReadyHampers_BudgetVnd", "\"BudgetVnd\" > 0"));
        });

        modelBuilder.Entity<ReadyHamperItem>(line =>
        {
            line.HasKey(l => new { l.ReadyHamperId, l.ProductId });
            // Deleting a ready hamper deletes its lines; an item that is in a ready hamper cannot be deleted by accident.
            line.HasOne(l => l.ReadyHamper).WithMany(h => h.Items).HasForeignKey(l => l.ReadyHamperId).OnDelete(DeleteBehavior.Cascade);
            line.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            line.ToTable(table => table.HasCheckConstraint("CK_ReadyHamperItems_Quantity", "\"Quantity\" > 0"));
        });

        modelBuilder.Entity<HamperItem>(line =>
        {
            line.HasKey(l => new { l.HamperId, l.ProductId });
            // Deleting a hamper deletes its lines; an item that is in someone's hamper cannot be deleted by accident.
            line.HasOne(l => l.Hamper).WithMany(h => h.Items).HasForeignKey(l => l.HamperId).OnDelete(DeleteBehavior.Cascade);
            line.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            line.ToTable(table => table.HasCheckConstraint("CK_HamperItems_Quantity", "\"Quantity\" > 0"));
        });
    }
}
