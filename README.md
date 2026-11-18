# Andrea Wear API

ASP.NET Core minimal API for the Andrea Wear storefront and its admin dashboard. It uses PostgreSQL with Entity Framework Core and applies pending migrations automatically at startup.

## Requirements

- .NET 10 SDK
- PostgreSQL 15 or compatible

## Run locally

1. Start PostgreSQL, or use Docker Compose:

   ```sh
   docker compose up --build
   ```

   The API is available at `http://localhost:5000` and PostgreSQL at `localhost:5432`.

2. To run the API with `dotnet run`, configure a PostgreSQL connection string. The development default is `Host=localhost;Port=5432;Database=andrea-wear-api;Username=postgres;Password=postgres`.

   ```sh
   dotnet restore
   dotnet run
   ```

The API reads `DATABASE_URL` when set; otherwise it uses `ConnectionStrings:DefaultConnection`. Set `Cors:AllowedOrigins` to the frontend origin(s). Admin login also requires the `Admin:AccessCode` setting, supplied as `Admin__AccessCode` in environment variables. Do not commit a production access code or database password.

## Features

- **Store** — product catalog, wishlist, checkout, and order tracking.
- **Admin** — cookie-based admin login, product/order/customer management, and dashboard data.
- **Persistence** — PostgreSQL entities, EF Core configurations, and migrations.

The API returns JSON in `snake_case`. Paginated results include `items`, `page`, `page_size`, and `total_count`. The health check is `GET /health`.

## Main routes

| Feature | Routes |
| --- | --- |
| Products | `GET /api/products`, `GET /api/products/{slug}` |
| Wishlist | `GET /api/wishlist`, `POST /api/wishlist/items`, `DELETE /api/wishlist/items/{productId}` |
| Orders | `POST /api/orders`, `GET /api/orders/track/{orderNumber}?email=...` |
| Admin auth | `POST /api/admin/auth/login`, `POST /api/admin/auth/logout` |
| Admin dashboard | `GET /api/admin/dashboard` |
| Admin catalog | `GET/POST /api/admin/products`, `PUT/DELETE /api/admin/products/{id}`, `PATCH /api/admin/products/{id}/active` |
| Admin orders | `GET /api/admin/orders`, `PATCH /api/admin/orders/{id}/status` |
| Admin customers | `GET /api/admin/customers` |

Admin routes, except login, require the admin session cookie. Browser clients should send requests with credentials enabled. Login is limited to five attempts per IP per minute.

## Project layout

```text
Features/
  Admin/       Admin endpoints, handlers, requests, responses, repositories, services
  Store/       Store endpoints, handlers, requests, responses, repositories, services
Entities/      Database entities
Infrastructure/ Database context, EF Core configurations, and shared infrastructure
Migrations/    EF Core database migrations
```

See [API.md](API.md) for endpoint details, request examples, and response behavior.
