using AndreaWear.Entities;
using Microsoft.EntityFrameworkCore;

namespace AndreaWearApi.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<StoreProduct> Products => Set<StoreProduct>();
    public DbSet<StoreCustomer> Customers => Set<StoreCustomer>();
    public DbSet<StoreOrder> Orders => Set<StoreOrder>();
    public DbSet<StoreOrderItem> OrderItems => Set<StoreOrderItem>();
    public DbSet<StoreWishlistItem> Wishlist => Set<StoreWishlistItem>();
    public DbSet<StoreSetting> Settings => Set<StoreSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
