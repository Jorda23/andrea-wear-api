using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AndreaWear.Entities;
using AndreaWearApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AndreaWearApi.Features.Store;

public static class StoreEndpoints
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private const decimal StandardShipping = 12m;
    private const decimal FreeShippingThreshold = 150m;
    private static readonly string[] Categories = ["women", "men", "accessories", "shoes", "general"];
    private static readonly string[] OrderStatuses = ["pending", "confirmed", "shipped", "delivered", "cancelled"];

    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Products");
        products.MapGet("", ListProductsAsync);
        products.MapGet("/{slug}", GetProductAsync);

        var wishlist = app.MapGroup("/api/wishlist").WithTags("Wishlist");
        wishlist.MapGet("", GetWishlistAsync);
        wishlist.MapPost("/items", AddWishlistItemAsync);
        wishlist.MapDelete("/items/{productId:guid}", DeleteWishlistItemAsync);

        var orders = app.MapGroup("/api/orders").WithTags("Orders");
        orders.MapPost("", CreateOrderAsync);
        orders.MapGet("/track/{orderNumber}", TrackOrderAsync);

        app.MapPost("/api/admin/auth/login", LoginAdminAsync)
            .RequireRateLimiting("admin-login")
            .WithTags("Admin Auth");
        var admin = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization();
        admin.MapPost("/auth/logout", (Delegate)LogoutAdminAsync);
        admin.MapGet("/dashboard", GetDashboardAsync);
        admin.MapGet("/products", ListAdminProductsAsync);
        admin.MapPost("/products", CreateProductAsync);
        admin.MapPut("/products/{id:guid}", UpdateProductAsync);
        admin.MapDelete("/products/{id:guid}", DeleteProductAsync);
        admin.MapPatch("/products/{id:guid}/active", SetProductActiveAsync);
        admin.MapGet("/orders", ListAdminOrdersAsync);
        admin.MapPatch("/orders/{id:guid}/status", SetOrderStatusAsync);
        admin.MapGet("/customers", ListCustomersAsync);
        return app;
    }

    private static async Task<IResult> ListProductsAsync(
        HttpRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var (page, pageSize) = ReadPage(request);
        var query = db.Products.AsNoTracking().Where(p => p.Active);
        var category = request.Query["category"].ToString();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category.Trim().ToLowerInvariant());
        if (bool.TryParse(request.Query["featured"], out var featured))
            query = query.Where(p => p.Featured == featured);
        var search = request.Query["search"].ToString().Trim();
        if (search.Length > 0)
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search}%") ||
                                     EF.Functions.ILike(p.Description, $"%{search}%"));

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return Results.Ok(new PageResponse<ProductResponse>(rows.Select(ToProduct).ToArray(), page, pageSize, total));
    }

    private static async Task<IResult> GetProductAsync(
        string slug, ApplicationDbContext db, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Active, ct);
        return product is null ? Results.NotFound() : Results.Ok(ToProduct(product));
    }

    private static async Task<IResult> GetWishlistAsync(
        HttpContext context, ApplicationDbContext db, CancellationToken ct)
    {
        var token = GetOrCreateVisitorToken(context);
        var ids = await db.Wishlist.AsNoTracking().Where(w => w.VisitorToken == token)
            .OrderByDescending(w => w.CreatedAt).Select(w => w.ProductId).ToListAsync(ct);
        return Results.Ok(ids);
    }

    private static async Task<IResult> AddWishlistItemAsync(
        CreateWishlistRequest? request, HttpContext context,
        ApplicationDbContext db, CancellationToken ct)
    {
        if (request is null || request.ProductId == Guid.Empty)
            return Validation("productId", "A valid productId is required.");
        var token = GetOrCreateVisitorToken(context);
        var exists = await db.Products.AnyAsync(p => p.Id == request.ProductId && p.Active, ct);
        if (!exists) return Results.NotFound(new { error = "Product not found." });

        var alreadySaved = await db.Wishlist.AnyAsync(
            w => w.VisitorToken == token && w.ProductId == request.ProductId, ct);
        if (!alreadySaved)
        {
            db.Wishlist.Add(new StoreWishlistItem { VisitorToken = token, ProductId = request.ProductId });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { }
        }
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteWishlistItemAsync(
        Guid productId, HttpContext context, ApplicationDbContext db, CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(VisitorCookieName, out var token))
            return Results.NoContent();
        await db.Wishlist.Where(w => w.VisitorToken == token && w.ProductId == productId)
            .ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateOrderAsync(
        CreateOrderRequest? request, ApplicationDbContext db, CancellationToken ct)
    {
        var invalid = ValidateOrder(request);
        if (invalid is not null) return invalid;
        var customerInput = request!.Customer!;
        var requestedItems = request.Items!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ids = requestedItems.Select(i => i.ProductId).Distinct().ToArray();
        var productRows = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (productRows.Count != ids.Length)
            return Validation("items", "One or more products are unavailable.");

        var lineData = new List<(CheckoutItemRequest Item, StoreProduct Product, decimal Price)>();
        foreach (var item in requestedItems)
        {
            var product = productRows[item.ProductId];
            if (!product.Active) return Validation("items", $"{product.Name} is unavailable.");
            if (!VariantAllowed(product.Sizes, item.Size) || !VariantAllowed(product.Colors, item.Color))
                return Validation("items", $"The selected size or color is not available for {product.Name}.");
            var price = product.SalePrice is >= 0 && product.SalePrice < product.Price
                ? product.SalePrice.Value
                : product.Price;
            lineData.Add((item, product, price));
        }

        return await CreateOrderInTransactionAsync(request, lineData, db, transaction, ct);
    }

    private static async Task<IResult> CreateOrderInTransactionAsync(
        CreateOrderRequest request,
        List<(CheckoutItemRequest Item, StoreProduct Product, decimal Price)> lineData,
        ApplicationDbContext db,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken ct)
    {
        foreach (var requirement in request.Items!.GroupBy(i => i.ProductId))
        {
            var amount = requirement.Sum(i => i.Quantity);
            var changed = await db.Products.Where(p => p.Id == requirement.Key && p.Active && p.Stock >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - amount), ct);
            if (changed != 1)
            {
                await transaction.RollbackAsync(ct);
                return Results.Conflict(new { error = "Insufficient stock for one or more products." });
            }
        }

        var input = request.Customer!;
        var email = input.Email!.Trim();
        var customer = await db.Customers.FirstOrDefaultAsync(
            c => c.Email.ToLower() == email.ToLower(), ct);
        if (customer is null)
        {
            customer = new StoreCustomer { Name = input.Name!.Trim(), Email = email };
            db.Customers.Add(customer);
        }
        customer.Name = input.Name!.Trim();
        customer.Email = email;
        customer.Phone = EmptyToNull(input.Phone);
        customer.AddressLine1 = input.ResolvedAddressLine1.Trim();
        customer.City = input.City!.Trim();
        customer.PostalCode = input.PostalCode!.Trim();
        customer.Country = input.Country!.Trim();

        var subtotal = lineData.Sum(line => line.Price * line.Item.Quantity);
        var shipping = subtotal >= FreeShippingThreshold ? 0m : StandardShipping;
        var order = new StoreOrder
        {
            OrderNumber = NewOrderNumber(),
            Customer = customer,
            ShippingName = customer.Name,
            ShippingEmail = customer.Email,
            ShippingPhone = customer.Phone,
            ShippingAddress = customer.AddressLine1,
            ShippingCity = customer.City,
            ShippingPostalCode = customer.PostalCode,
            ShippingCountry = customer.Country,
            Subtotal = subtotal,
            ShippingCost = shipping,
            Total = subtotal + shipping,
            Status = "pending",
            Items = lineData.Select(line => new StoreOrderItem
            {
                ProductId = line.Product.Id,
                ProductName = line.Product.Name,
                Size = EmptyToNull(line.Item.Size),
                Color = EmptyToNull(line.Item.Color),
                UnitPrice = line.Price,
                Quantity = line.Item.Quantity
            }).ToList()
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/orders/track/{order.OrderNumber}", ToOrder(order));
    }

    private static async Task<IResult> TrackOrderAsync(
        string orderNumber, string email, ApplicationDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Validation("email", "A valid checkout email is required to track this order.");
        var normalizedNumber = orderNumber.Trim().ToUpperInvariant();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == normalizedNumber &&
                                      o.ShippingEmail.ToLower() == normalizedEmail, ct);
        if (order is null) return Results.NotFound();
        return Results.Ok(new TrackOrderResponse(order.OrderNumber, order.Status,
            order.CreatedAt, order.Subtotal, order.ShippingCost, order.Total,
            order.Items.Select(ToOrderItem).ToArray()));
    }

    private static async Task<IResult> LoginAdminAsync(
        AdminLoginRequest? request, HttpContext context, IConfiguration configuration)
    {
        var expectedCode = configuration["Admin:AccessCode"];
        if (string.IsNullOrWhiteSpace(expectedCode))
            return Results.Problem("Admin access is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        if (request?.Code is null || !FixedTimeEquals(request.Code, expectedCode))
            return Results.Unauthorized();

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "store-admin"), new Claim(ClaimTypes.Role, "admin")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAdminAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetDashboardAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var recent = await db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt)
            .Take(8).ToListAsync(ct);
        return Results.Ok(new AdminDashboardResponse(
            await db.Products.CountAsync(ct), await db.Products.CountAsync(p => p.Active, ct),
            await db.Orders.CountAsync(ct), await db.Orders.CountAsync(o => o.Status == "pending", ct),
            await db.Customers.CountAsync(ct), recent.Select(ToOrder).ToArray()));
    }

    private static async Task<IResult> ListAdminProductsAsync(
        HttpRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var (page, pageSize) = ReadPage(request);
        var query = db.Products.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<ProductResponse>(rows.Select(ToProduct).ToArray(), page, pageSize, total));
    }

    private static async Task<IResult> CreateProductAsync(
        ProductWriteRequest? request, ApplicationDbContext db, CancellationToken ct)
    {
        var error = ValidateProduct(request);
        if (error is not null) return error;
        var product = new StoreProduct { Name = request!.Name!, Slug = request.Slug! };
        ApplyProduct(product, request!);
        db.Products.Add(product);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        { return Results.Conflict(new { error = "A product with this slug already exists." }); }
        return Results.Created($"/api/admin/products/{product.Id}", ToProduct(product));
    }

    private static async Task<IResult> UpdateProductAsync(
        Guid id, ProductWriteRequest? request, ApplicationDbContext db, CancellationToken ct)
    {
        var error = ValidateProduct(request);
        if (error is not null) return error;
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Results.NotFound();
        ApplyProduct(product, request!);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        { return Results.Conflict(new { error = "A product with this slug already exists." }); }
        return Results.Ok(ToProduct(product));
    }

    private static async Task<IResult> DeleteProductAsync(
        Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Results.NotFound();
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetProductActiveAsync(
        Guid id, SetProductActiveRequest? request, ApplicationDbContext db, CancellationToken ct)
    {
        if (request is null) return Validation("active", "The active field is required.");
        var updated = await db.Products.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Active, request.Active), ct);
        return updated == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static async Task<IResult> ListAdminOrdersAsync(
        HttpRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var (page, pageSize) = ReadPage(request);
        var query = db.Orders.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rows = await query.Include(o => o.Items).OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<OrderWithItemsResponse>(rows.Select(ToOrderWithItems).ToArray(), page, pageSize, total));
    }

    private static async Task<IResult> SetOrderStatusAsync(
        Guid id, UpdateOrderStatusRequest? request, ApplicationDbContext db, CancellationToken ct)
    {
        var status = request?.Status?.Trim().ToLowerInvariant();
        if (status is null || !OrderStatuses.Contains(status))
            return Validation("status", "Status must be pending, confirmed, shipped, delivered, or cancelled.");
        var updated = await db.Orders.Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status), ct);
        return updated == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static async Task<IResult> ListCustomersAsync(
        HttpRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var (page, pageSize) = ReadPage(request);
        var query = db.Customers.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var customers = rows.Select(c => new CustomerResponse(c.Id, c.Name, c.Email,
            c.Phone, c.AddressLine1, c.City, c.PostalCode, c.Country, c.CreatedAt)).ToArray();
        return Results.Ok(new PageResponse<CustomerResponse>(customers, page, pageSize, total));
    }

    private static IResult? ValidateOrder(CreateOrderRequest? request)
    {
        if (request?.Customer is null) return Validation("customer", "Customer details are required.");
        var c = request.Customer;
        if (string.IsNullOrWhiteSpace(c.Name)) return Validation("customer.name", "Name is required.");
        if (string.IsNullOrWhiteSpace(c.Email) || !c.Email.Contains('@')) return Validation("customer.email", "A valid email is required.");
        if (string.IsNullOrWhiteSpace(c.ResolvedAddressLine1)) return Validation("customer.addressLine1", "Address is required.");
        if (string.IsNullOrWhiteSpace(c.City)) return Validation("customer.city", "City is required.");
        if (string.IsNullOrWhiteSpace(c.PostalCode)) return Validation("customer.postalCode", "Postal code is required.");
        if (string.IsNullOrWhiteSpace(c.Country)) return Validation("customer.country", "Country is required.");
        if (request.Items is null || request.Items.Count is < 1 or > 50)
            return Validation("items", "Provide between 1 and 50 order items.");
        foreach (var item in request.Items)
            if (item.ProductId == Guid.Empty || item.Quantity is < 1 or > 100)
                return Validation("items", "Each item requires a productId and quantity between 1 and 100.");
        return null;
    }

    private static IResult? ValidateProduct(ProductWriteRequest? p)
    {
        if (p is null) return Validation("product", "Product data is required.");
        if (string.IsNullOrWhiteSpace(p.Name)) return Validation("name", "Name is required.");
        if (string.IsNullOrWhiteSpace(p.Slug) || p.Slug.Length > 200) return Validation("slug", "A slug up to 200 characters is required.");
        if (p.Price < 0 || (p.SalePrice is < 0)) return Validation("price", "Prices cannot be negative.");
        if (p.Stock < 0) return Validation("stock", "Stock cannot be negative.");
        if (string.IsNullOrWhiteSpace(p.Category) || !Categories.Contains(p.Category.Trim().ToLowerInvariant()))
            return Validation("category", "Category must be women, men, accessories, shoes, or general.");
        if ((p.Colors?.Length ?? 0) > 50 || (p.Sizes?.Length ?? 0) > 50)
            return Validation("variants", "A product can have at most 50 colors and 50 sizes.");
        return null;
    }

    private static void ApplyProduct(StoreProduct product, ProductWriteRequest request)
    {
        product.Name = request.Name!.Trim();
        product.Slug = request.Slug!.Trim().ToLowerInvariant();
        product.Description = request.Description ?? string.Empty;
        product.Price = decimal.Round(request.Price, 2);
        product.SalePrice = request.SalePrice is null ? null : decimal.Round(request.SalePrice.Value, 2);
        product.Category = request.Category!.Trim().ToLowerInvariant();
        product.Colors = CleanValues(request.Colors);
        product.Sizes = CleanValues(request.Sizes);
        product.Stock = request.Stock;
        product.ImageUrl = EmptyToNull(request.ImageUrl);
        product.Featured = request.Featured;
        product.Active = request.Active;
    }

    private static List<string> CleanValues(string[]? values) => values is null
        ? [] : values.Select(v => v.Trim()).Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static bool VariantAllowed(IReadOnlyCollection<string> available, string? requested) =>
        string.IsNullOrWhiteSpace(requested)
            ? available.Count == 0
            : available.Contains(requested.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string GetOrCreateVisitorToken(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(VisitorCookieName, out var existing) &&
            Guid.TryParseExact(existing, "N", out _)) return existing;
        var token = Guid.NewGuid().ToString("N");
        context.Response.Cookies.Append(VisitorCookieName, token, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.None,
            IsEssential = true, MaxAge = TimeSpan.FromDays(365), Path = "/api/wishlist"
        });
        return token;
    }

    private const string VisitorCookieName = ".AndreaWearApi.Visitor";

    private static (int Page, int PageSize) ReadPage(HttpRequest request)
    {
        var page = int.TryParse(request.Query["page"], out var p) ? Math.Clamp(p, 1, 1_000_000) : 1;
        var pageSize = int.TryParse(request.Query["pageSize"], out var ps)
            ? Math.Clamp(ps, 1, MaximumPageSize) : DefaultPageSize;
        return (page, pageSize);
    }

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NewOrderNumber() =>
        $"AW-{DateTime.UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(5))}";

    private static bool FixedTimeEquals(string submitted, string expected)
    {
        var submittedHash = SHA256.HashData(Encoding.UTF8.GetBytes(submitted));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(submittedHash, expectedHash);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static ProductResponse ToProduct(StoreProduct p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.Price, p.SalePrice,
        p.Category, p.Colors.ToArray(), p.Sizes.ToArray(), p.Stock,
        p.ImageUrl, p.Featured, p.Active, p.CreatedAt);

    private static OrderResponse ToOrder(StoreOrder o) => new(
        o.Id, o.OrderNumber, o.CustomerId, o.ShippingName, o.ShippingEmail,
        o.ShippingPhone, o.ShippingAddress, o.ShippingCity, o.ShippingPostalCode,
        o.ShippingCountry, o.Subtotal, o.ShippingCost, o.Total, o.Status, o.CreatedAt);

    private static OrderItemResponse ToOrderItem(StoreOrderItem i) => new(
        i.Id, i.OrderId, i.ProductId, i.ProductName, i.Size, i.Color, i.UnitPrice, i.Quantity);

    private static OrderWithItemsResponse ToOrderWithItems(StoreOrder o) => new(
        o.Id, o.OrderNumber, o.CustomerId, o.ShippingName, o.ShippingEmail,
        o.ShippingPhone, o.ShippingAddress, o.ShippingCity, o.ShippingPostalCode,
        o.ShippingCountry, o.Subtotal, o.ShippingCost, o.Total, o.Status, o.CreatedAt,
        o.Items.Select(ToOrderItem).ToArray());
}
