using Microsoft.EntityFrameworkCore;

namespace Peekbox.Web.Data;

public class PeekboxDbContext(DbContextOptions<PeekboxDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Hamper> Hampers => Set<Hamper>();

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
            hamper.ToTable(table => table.HasCheckConstraint("CK_Hampers_BudgetVnd", "\"BudgetVnd\" >= 0"));
        });
    }
}
