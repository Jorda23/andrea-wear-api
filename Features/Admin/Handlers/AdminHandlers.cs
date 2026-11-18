using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AndreaWear.Entities;
using AndreaWearApi.Features.Admin.Requests;
using AndreaWearApi.Features.Admin.Responses;
using AndreaWearApi.Features.Admin.Services;
using AndreaWearApi.Features.Store.Requests;
using AndreaWearApi.Features.Store.Responses;
using AndreaWearApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AndreaWearApi.Features.Admin;

public static class AdminHandlers
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private static readonly string[] Categories = ["women", "men", "accessories", "shoes", "general"];
    private static readonly string[] OrderStatuses = ["pending", "confirmed", "shipped", "delivered", "cancelled"];

    public static async Task<IResult> LoginAdminAsync(
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

    public static async Task<IResult> LogoutAdminAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    public static async Task<IResult> GetDashboardAsync(IAdminDashboardService dashboard, CancellationToken ct) =>
        Results.Ok(await dashboard.GetDashboardAsync(ct));

    public static async Task<IResult> ListAdminProductsAsync(
        HttpRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var (page, pageSize) = ReadPage(request);
        var query = db.Products.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<ProductResponse>(rows.Select(ToProduct).ToArray(), page, pageSize, total));
    }

    public static async Task<IResult> CreateProductAsync(
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

    public static async Task<IResult> UpdateProductAsync(
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

    public static async Task<IResult> DeleteProductAsync(
        Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Results.NotFound();
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    public static async Task<IResult> SetProductActiveAsync(
        Guid id, SetProductActiveRequest? request, ApplicationDbContext db, CancellationToken ct)
    {
        if (request is null) return Validation("active", "The active field is required.");
        var updated = await db.Products.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Active, request.Active), ct);
        return updated == 0 ? Results.NotFound() : Results.NoContent();
    }

    public static async Task<IResult> ListAdminOrdersAsync(
        HttpRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var (page, pageSize) = ReadPage(request);
        var query = db.Orders.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rows = await query.Include(o => o.Items).OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new PageResponse<OrderWithItemsResponse>(rows.Select(ToOrderWithItems).ToArray(), page, pageSize, total));
    }

    public static async Task<IResult> SetOrderStatusAsync(
        Guid id, UpdateOrderStatusRequest? request, ApplicationDbContext db, CancellationToken ct)
    {
        var status = request?.Status?.Trim().ToLowerInvariant();
        if (status is null || !OrderStatuses.Contains(status))
            return Validation("status", "Status must be pending, confirmed, shipped, delivered, or cancelled.");
        var updated = await db.Orders.Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status), ct);
        return updated == 0 ? Results.NotFound() : Results.NoContent();
    }

    public static async Task<IResult> ListCustomersAsync(
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
