using AndreaWear.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreaWearApi.Infrastructure.Persistence.Configurations;

public sealed class StoreProductConfiguration : IEntityTypeConfiguration<StoreProduct>
{
    public void Configure(EntityTypeBuilder<StoreProduct> b)
    {
        b.ToTable("products");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Slug).HasColumnName("slug").IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Description).HasColumnName("description").HasDefaultValue("");
        b.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
        b.ToTable(t => t.HasCheckConstraint("ck_products_price_nonnegative", "price >= 0"));
        b.Property(x => x.SalePrice).HasColumnName("sale_price").HasPrecision(10, 2);
        b.ToTable(t => t.HasCheckConstraint("ck_products_sale_price_nonnegative", "sale_price IS NULL OR sale_price >= 0"));
        b.Property(x => x.Category).HasColumnName("category").HasDefaultValue("general");
        b.Property(x => x.Colors).HasColumnName("colors").HasColumnType("text[]").HasDefaultValueSql("'{}'::text[]");
        b.Property(x => x.Sizes).HasColumnName("sizes").HasColumnType("text[]").HasDefaultValueSql("'{}'::text[]");
        b.Property(x => x.Stock).HasColumnName("stock").HasDefaultValue(0);
        b.ToTable(t => t.HasCheckConstraint("ck_products_stock_nonnegative", "stock >= 0"));
        b.Property(x => x.ImageUrl).HasColumnName("image_url");
        b.Property(x => x.Featured).HasColumnName("featured").HasDefaultValue(false);
        b.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        b.HasIndex(x => x.Category).HasDatabaseName("idx_products_category");
        b.HasIndex(x => x.Active).HasDatabaseName("idx_products_active");
    }
}

public sealed class StoreCustomerConfiguration : IEntityTypeConfiguration<StoreCustomer>
{
    public void Configure(EntityTypeBuilder<StoreCustomer> b)
    {
        b.ToTable("customers"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Name).HasColumnName("name").IsRequired();
        b.Property(x => x.Email).HasColumnName("email").IsRequired();
        b.Property(x => x.Phone).HasColumnName("phone");
        b.Property(x => x.AddressLine1).HasColumnName("address_line1").HasDefaultValue("");
        b.Property(x => x.City).HasColumnName("city").HasDefaultValue("");
        b.Property(x => x.PostalCode).HasColumnName("postal_code").HasDefaultValue("");
        b.Property(x => x.Country).HasColumnName("country").HasDefaultValue("");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
    }
}

public sealed class StoreOrderConfiguration : IEntityTypeConfiguration<StoreOrder>
{
    public void Configure(EntityTypeBuilder<StoreOrder> b)
    {
        b.ToTable("orders", t => t.HasCheckConstraint("ck_orders_status", "status IN ('pending','confirmed','shipped','delivered','cancelled')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.OrderNumber).HasColumnName("order_number").IsRequired();
        b.HasIndex(x => x.OrderNumber).IsUnique();
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.ShippingName).HasColumnName("shipping_name").IsRequired();
        b.Property(x => x.ShippingEmail).HasColumnName("shipping_email").IsRequired();
        b.Property(x => x.ShippingPhone).HasColumnName("shipping_phone");
        b.Property(x => x.ShippingAddress).HasColumnName("shipping_address").HasDefaultValue("");
        b.Property(x => x.ShippingCity).HasColumnName("shipping_city").HasDefaultValue("");
        b.Property(x => x.ShippingPostalCode).HasColumnName("shipping_postal_code").HasDefaultValue("");
        b.Property(x => x.ShippingCountry).HasColumnName("shipping_country").HasDefaultValue("");
        b.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(10, 2).HasDefaultValue(0m);
        b.Property(x => x.ShippingCost).HasColumnName("shipping_cost").HasPrecision(10, 2).HasDefaultValue(0m);
        b.Property(x => x.Total).HasColumnName("total").HasPrecision(10, 2).HasDefaultValue(0m);
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue("pending");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        b.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.CreatedAt).IsDescending().HasDatabaseName("idx_orders_created");
    }
}

public sealed class StoreOrderItemConfiguration : IEntityTypeConfiguration<StoreOrderItem>
{
    public void Configure(EntityTypeBuilder<StoreOrderItem> b)
    {
        b.ToTable("order_items", t => t.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.ProductName).HasColumnName("product_name").IsRequired();
        b.Property(x => x.Size).HasColumnName("size");
        b.Property(x => x.Color).HasColumnName("color");
        b.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(10, 2);
        b.Property(x => x.Quantity).HasColumnName("quantity");
        b.HasIndex(x => x.OrderId).HasDatabaseName("idx_order_items_order");
    }
}

public sealed class StoreWishlistItemConfiguration : IEntityTypeConfiguration<StoreWishlistItem>
{
    public void Configure(EntityTypeBuilder<StoreWishlistItem> b)
    {
        b.ToTable("wishlist"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.VisitorToken).HasColumnName("visitor_token").IsRequired();
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        b.HasIndex(x => new { x.VisitorToken, x.ProductId }).IsUnique();
        b.HasIndex(x => x.VisitorToken).HasDatabaseName("idx_wishlist_token");
    }
}

public sealed class StoreSettingConfiguration : IEntityTypeConfiguration<StoreSetting>
{
    public void Configure(EntityTypeBuilder<StoreSetting> b)
    {
        b.ToTable("settings");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasColumnName("key");
        b.Property(x => x.Value).HasColumnName("value").IsRequired();
    }
}
