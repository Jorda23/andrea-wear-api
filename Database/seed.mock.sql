-- Sample catalog, customers, orders, order lines, and wishlist entries.
-- Safe to run repeatedly against the Andrea Wear schema.
BEGIN;

INSERT INTO products
    (id, name, slug, description, price, sale_price, category, colors, sizes,
     stock, image_url, featured, active)
VALUES
    ('a1000000-0000-4000-8000-000000000001', 'Linen Wrap Dress', 'linen-wrap-dress',
     'A breathable linen midi dress with a flattering wrap silhouette.', 89.00, 74.00, 'women',
     ARRAY['Sand', 'Olive'], ARRAY['XS', 'S', 'M', 'L'], 18,
     'https://images.unsplash.com/photo-1595777457583-95e059d581b8', true, true),
    ('a1000000-0000-4000-8000-000000000002', 'Everyday Cotton Tee', 'everyday-cotton-tee',
     'A soft, relaxed-fit cotton t-shirt for everyday wear.', 32.00, NULL, 'women',
     ARRAY['White', 'Black', 'Rose'], ARRAY['XS', 'S', 'M', 'L', 'XL'], 42,
     'https://images.unsplash.com/photo-1521572163474-6864f9cf17ab', false, true),
    ('a1000000-0000-4000-8000-000000000003', 'Classic Oxford Shirt', 'classic-oxford-shirt',
     'A crisp cotton Oxford shirt with a clean, tailored finish.', 68.00, NULL, 'men',
     ARRAY['White', 'Sky Blue'], ARRAY['S', 'M', 'L', 'XL'], 24,
     'https://images.unsplash.com/photo-1596755094514-f87e34085b2c', true, true),
    ('a1000000-0000-4000-8000-000000000004', 'Tapered Chino Pants', 'tapered-chino-pants',
     'Versatile mid-rise chinos in a comfortable cotton stretch fabric.', 76.00, 64.00, 'men',
     ARRAY['Navy', 'Khaki'], ARRAY['30', '32', '34', '36'], 16,
     'https://images.unsplash.com/photo-1473966968600-fa801b869a1a', false, true),
    ('a1000000-0000-4000-8000-000000000005', 'Mini Crossbody Bag', 'mini-crossbody-bag',
     'A compact everyday bag with an adjustable strap and zip closure.', 58.00, NULL, 'accessories',
     ARRAY['Tan', 'Black'], ARRAY['One Size'], 13,
     'https://images.unsplash.com/photo-1548036328-c9fa89d128fa', true, true),
    ('a1000000-0000-4000-8000-000000000006', 'Minimal Gold Hoops', 'minimal-gold-hoops',
     'Lightweight gold-tone hoops for a simple, polished look.', 28.00, NULL, 'accessories',
     ARRAY['Gold'], ARRAY['One Size'], 31,
     'https://images.unsplash.com/photo-1535632066927-ab7c9ab60908', false, true),
    ('a1000000-0000-4000-8000-000000000007', 'Leather Everyday Sneakers', 'leather-everyday-sneakers',
     'Low-profile leather sneakers with a cushioned sole.', 96.00, 82.00, 'shoes',
     ARRAY['White', 'Cream'], ARRAY['6', '7', '8', '9', '10'], 12,
     'https://images.unsplash.com/photo-1542291026-7eec264c27ff', true, true),
    ('a1000000-0000-4000-8000-000000000008', 'Suede Ankle Boots', 'suede-ankle-boots',
     'Soft suede ankle boots with a low stacked heel.', 124.00, NULL, 'shoes',
     ARRAY['Cognac', 'Black'], ARRAY['6', '7', '8', '9', '10'], 8,
     'https://images.unsplash.com/photo-1608256246200-53e635b5b65f', false, true)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name, slug = EXCLUDED.slug, description = EXCLUDED.description,
    price = EXCLUDED.price, sale_price = EXCLUDED.sale_price, category = EXCLUDED.category,
    colors = EXCLUDED.colors, sizes = EXCLUDED.sizes, stock = EXCLUDED.stock,
    image_url = EXCLUDED.image_url, featured = EXCLUDED.featured, active = EXCLUDED.active;

INSERT INTO customers
    (id, name, email, phone, address_line1, city, postal_code, country)
VALUES
    ('b2000000-0000-4000-8000-000000000001', 'Sofia Martinez', 'sofia@example.com',
     '+1-555-0101', '125 Maple Street', 'Austin', '78701', 'United States'),
    ('b2000000-0000-4000-8000-000000000002', 'Daniel Brooks', 'daniel@example.com',
     '+1-555-0102', '48 Pine Avenue', 'Portland', '97205', 'United States')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name, email = EXCLUDED.email, phone = EXCLUDED.phone,
    address_line1 = EXCLUDED.address_line1, city = EXCLUDED.city,
    postal_code = EXCLUDED.postal_code, country = EXCLUDED.country;

INSERT INTO orders
    (id, order_number, customer_id, shipping_name, shipping_email, shipping_phone,
     shipping_address, shipping_city, shipping_postal_code, shipping_country,
     subtotal, shipping_cost, total, status)
VALUES
    ('c3000000-0000-4000-8000-000000000001', 'AW-DEMO-1001',
     'b2000000-0000-4000-8000-000000000001', 'Sofia Martinez', 'sofia@example.com',
     '+1-555-0101', '125 Maple Street', 'Austin', '78701', 'United States',
     102.00, 12.00, 114.00, 'pending'),
    ('c3000000-0000-4000-8000-000000000002', 'AW-DEMO-1002',
     'b2000000-0000-4000-8000-000000000002', 'Daniel Brooks', 'daniel@example.com',
     '+1-555-0102', '48 Pine Avenue', 'Portland', '97205', 'United States',
     208.00, 0.00, 208.00, 'confirmed')
ON CONFLICT (id) DO UPDATE SET
    order_number = EXCLUDED.order_number, customer_id = EXCLUDED.customer_id,
    shipping_name = EXCLUDED.shipping_name, shipping_email = EXCLUDED.shipping_email,
    shipping_phone = EXCLUDED.shipping_phone, shipping_address = EXCLUDED.shipping_address,
    shipping_city = EXCLUDED.shipping_city, shipping_postal_code = EXCLUDED.shipping_postal_code,
    shipping_country = EXCLUDED.shipping_country, subtotal = EXCLUDED.subtotal,
    shipping_cost = EXCLUDED.shipping_cost, total = EXCLUDED.total, status = EXCLUDED.status;

INSERT INTO order_items (id, order_id, product_id, product_name, size, color, unit_price, quantity)
VALUES
    ('d4000000-0000-4000-8000-000000000001', 'c3000000-0000-4000-8000-000000000001',
     'a1000000-0000-4000-8000-000000000001', 'Linen Wrap Dress', 'M', 'Sand', 74.00, 1),
    ('d4000000-0000-4000-8000-000000000002', 'c3000000-0000-4000-8000-000000000001',
     'a1000000-0000-4000-8000-000000000006', 'Minimal Gold Hoops', 'One Size', 'Gold', 28.00, 1),
    ('d4000000-0000-4000-8000-000000000003', 'c3000000-0000-4000-8000-000000000002',
     'a1000000-0000-4000-8000-000000000003', 'Classic Oxford Shirt', 'L', 'Sky Blue', 68.00, 1),
    ('d4000000-0000-4000-8000-000000000004', 'c3000000-0000-4000-8000-000000000002',
     'a1000000-0000-4000-8000-000000000007', 'Leather Everyday Sneakers', '9', 'White', 82.00, 1),
    ('d4000000-0000-4000-8000-000000000005', 'c3000000-0000-4000-8000-000000000002',
     'a1000000-0000-4000-8000-000000000005', 'Mini Crossbody Bag', 'One Size', 'Tan', 58.00, 1)
ON CONFLICT (id) DO UPDATE SET
    order_id = EXCLUDED.order_id, product_id = EXCLUDED.product_id,
    product_name = EXCLUDED.product_name, size = EXCLUDED.size, color = EXCLUDED.color,
    unit_price = EXCLUDED.unit_price, quantity = EXCLUDED.quantity;

INSERT INTO wishlist (id, visitor_token, product_id)
VALUES
    ('e5000000-0000-4000-8000-000000000001', 'demo-visitor-sofia', 'a1000000-0000-4000-8000-000000000007'),
    ('e5000000-0000-4000-8000-000000000002', 'demo-visitor-sofia', 'a1000000-0000-4000-8000-000000000005'),
    ('e5000000-0000-4000-8000-000000000003', 'demo-visitor-daniel', 'a1000000-0000-4000-8000-000000000001')
ON CONFLICT (id) DO UPDATE SET
    visitor_token = EXCLUDED.visitor_token, product_id = EXCLUDED.product_id;

INSERT INTO settings (key, value)
VALUES ('store_name', 'Andrea Wear'), ('currency', 'USD')
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value;

COMMIT;
