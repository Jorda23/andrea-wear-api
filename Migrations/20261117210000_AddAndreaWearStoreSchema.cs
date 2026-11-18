using AndreaWearApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AndreaWearApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261117210000_AddAndreaWearStoreSchema")]
public sealed class AddAndreaWearStoreSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE products (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            name text NOT NULL,
            slug text NOT NULL UNIQUE,
            description text NOT NULL DEFAULT '',
            price numeric(10,2) NOT NULL CHECK (price >= 0),
            sale_price numeric(10,2) CHECK (sale_price IS NULL OR sale_price >= 0),
            category text NOT NULL DEFAULT 'general',
            colors text[] NOT NULL DEFAULT '{}',
            sizes text[] NOT NULL DEFAULT '{}',
            stock integer NOT NULL DEFAULT 0 CHECK (stock >= 0),
            image_url text,
            featured boolean NOT NULL DEFAULT false,
            active boolean NOT NULL DEFAULT true,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE customers (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            name text NOT NULL, email text NOT NULL, phone text,
            address_line1 text NOT NULL DEFAULT '', city text NOT NULL DEFAULT '',
            postal_code text NOT NULL DEFAULT '', country text NOT NULL DEFAULT '',
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE orders (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            order_number text NOT NULL UNIQUE,
            customer_id uuid REFERENCES customers(id) ON DELETE SET NULL,
            shipping_name text NOT NULL, shipping_email text NOT NULL, shipping_phone text,
            shipping_address text NOT NULL DEFAULT '', shipping_city text NOT NULL DEFAULT '',
            shipping_postal_code text NOT NULL DEFAULT '', shipping_country text NOT NULL DEFAULT '',
            subtotal numeric(10,2) NOT NULL DEFAULT 0,
            shipping_cost numeric(10,2) NOT NULL DEFAULT 0,
            total numeric(10,2) NOT NULL DEFAULT 0,
            status text NOT NULL DEFAULT 'pending'
                CHECK (status IN ('pending','confirmed','shipped','delivered','cancelled')),
            created_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE order_items (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            order_id uuid NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
            product_id uuid REFERENCES products(id) ON DELETE SET NULL,
            product_name text NOT NULL, size text, color text,
            unit_price numeric(10,2) NOT NULL,
            quantity integer NOT NULL CHECK (quantity > 0)
        );
        CREATE TABLE wishlist (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            visitor_token text NOT NULL,
            product_id uuid NOT NULL REFERENCES products(id) ON DELETE CASCADE,
            created_at timestamptz NOT NULL DEFAULT now(),
            CONSTRAINT uq_wishlist_visitor_product UNIQUE (visitor_token, product_id)
        );
        CREATE TABLE settings (key text PRIMARY KEY, value text NOT NULL);
        CREATE INDEX idx_products_category ON products(category);
        CREATE INDEX idx_products_active ON products(active);
        CREATE INDEX idx_orders_created ON orders(created_at DESC);
        CREATE INDEX idx_order_items_order ON order_items(order_id);
        CREATE INDEX idx_wishlist_token ON wishlist(visitor_token);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE wishlist;
        DROP TABLE order_items;
        DROP TABLE orders;
        DROP TABLE customers;
        DROP TABLE settings;
        DROP TABLE products;
        """);
}
