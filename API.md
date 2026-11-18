# Andrea Wear API

The API applies EF Core migrations during startup. Responses use `snake_case` and paginated routes return `{ "items": [], "page": 1, "page_size": 24, "total_count": 0 }`.

## Configuration

Set `Admin__AccessCode` to a long, private value on the API host. It is intentionally not stored in `appsettings.json`. Set `Cors__AllowedOrigins__0` (and additional numbered entries) to the exact frontend origins. The local default is `http://localhost:3000`.

The browser must use `credentials: "include"` for wishlist and admin requests. Admin and visitor cookies are `HttpOnly`, `Secure`, and `SameSite=None`; use HTTPS outside localhost.

## Storefront

- `GET /api/products?category=women&featured=true&search=linen&page=1&pageSize=24`
- `GET /api/products/{slug}` — returns `404` for missing or inactive products.
- `GET /api/wishlist` — returns saved product IDs and sets a visitor cookie on first use.
- `POST /api/wishlist/items` — body: `{ "productId": "uuid" }`; duplicate saves are idempotent.
- `DELETE /api/wishlist/items/{productId}`
- `POST /api/orders` — creates a pending order. Prices, totals, shipping, variants, and stock are validated/calculated by the API. Shipping is free for subtotals of `$150` or more, otherwise `$12`.
- `GET /api/orders/track/{orderNumber}?email=buyer@example.com` — email must match the checkout email; response omits shipping address and other customer details.

Checkout request example:

```json
{
  "customer": {
    "name": "Jane Smith",
    "email": "jane@example.com",
    "phone": "+1 555 000 1234",
    "addressLine1": "123 Maple Street",
    "city": "New York",
    "postalCode": "10001",
    "country": "United States"
  },
  "items": [
    { "productId": "product-uuid", "size": "M", "color": "Black", "quantity": 1 }
  ]
}
```

## Admin

1. `POST /api/admin/auth/login` with `{ "code": "..." }` to create the admin session cookie.
2. Use the cookie for all `/api/admin/*` endpoints; login is rate limited to five attempts per IP per minute.
3. `POST /api/admin/auth/logout` clears the session.

Protected endpoints:

- `GET /api/admin/dashboard`
- `GET /api/admin/products?page=1&pageSize=24`
- `POST /api/admin/products`
- `PUT /api/admin/products/{id}`
- `DELETE /api/admin/products/{id}`
- `PATCH /api/admin/products/{id}/active` with `{ "active": false }`
- `GET /api/admin/orders?page=1&pageSize=24`
- `PATCH /api/admin/orders/{id}/status` with `{ "status": "shipped" }`
- `GET /api/admin/customers?page=1&pageSize=24`

Admin product writes accept the product fields from the frontend `Product` type (including `sale_price`, `image_url`, `colors`, and `sizes`).
