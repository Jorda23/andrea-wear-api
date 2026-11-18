using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AndreaWear.Entities;
using AndreaWearApi.Infrastructure.Persistence;
using AndreaWearApi.Features.Store.Requests;
using AndreaWearApi.Features.Store.Responses;
using AndreaWearApi.Features.Store.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AndreaWearApi.Features.Store;

public static class StoreHandlers
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private const decimal StandardShipping = 12m;
    private const decimal FreeShippingThreshold = 150m;
    private static readonly string[] OrderStatuses = ["pending", "confirmed", "shipped", "delivered", "cancelled"];



    public static async Task<IResult> ListProductsAsync(
        HttpRequest request, IStoreProductService products, CancellationToken ct)
    {
        var (page, pageSize) = ReadPage(request);
        var category = request.Query["category"].ToString();
        bool? featured = bool.TryParse(request.Query["featured"], out var parsedFeatured) ? parsedFeatured : null;
        var search = request.Query["search"].ToString().Trim();
        return Results.Ok(await products.ListActiveAsync(category, featured, search, page, pageSize, ct));
    }

    public static async Task<IResult> GetProductAsync(
        string slug, IStoreProductService products, CancellationToken ct)
    {
        var product = await products.FindActiveBySlugAsync(slug, ct);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    public static async Task<IResult> GetWishlistAsync(
        HttpContext context, ApplicationDbContext db, CancellationToken ct)
    {
        var token = GetOrCreateVisitorToken(context);
        var ids = await db.Wishlist.AsNoTracking().Where(w => w.VisitorToken == token)
            .OrderByDescending(w => w.CreatedAt).Select(w => w.ProductId).ToListAsync(ct);
        return Results.Ok(ids);
    }

    public static async Task<IResult> AddWishlistItemAsync(
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

    public static async Task<IResult> DeleteWishlistItemAsync(
        Guid productId, HttpContext context, ApplicationDbContext db, CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(VisitorCookieName, out var token))
            return Results.NoContent();
        await db.Wishlist.Where(w => w.VisitorToken == token && w.ProductId == productId)
            .ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    public static async Task<IResult> CreateOrderAsync(
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

    public static async Task<IResult> TrackOrderAsync(
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

    private static OrderResponse ToOrder(StoreOrder o) => new(
        o.Id, o.OrderNumber, o.CustomerId, o.ShippingName, o.ShippingEmail,
        o.ShippingPhone, o.ShippingAddress, o.ShippingCity, o.ShippingPostalCode,
        o.ShippingCountry, o.Subtotal, o.ShippingCost, o.Total, o.Status, o.CreatedAt);

    private static OrderItemResponse ToOrderItem(StoreOrderItem i) => new(
        i.Id, i.OrderId, i.ProductId, i.ProductName, i.Size, i.Color, i.UnitPrice, i.Quantity);

}
