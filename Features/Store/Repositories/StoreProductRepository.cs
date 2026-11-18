using AndreaWear.Entities;
using AndreaWearApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AndreaWearApi.Features.Store.Repositories;

public sealed class StoreProductRepository(ApplicationDbContext db) : IStoreProductRepository
{
    public async Task<(IReadOnlyList<StoreProduct> Products, int TotalCount)> ListActiveAsync(
        string? category, bool? featured, string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking().Where(p => p.Active);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category.Trim().ToLowerInvariant());
        if (featured.HasValue)
            query = query.Where(p => p.Featured == featured.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{term}%") ||
                                     EF.Functions.ILike(p.Description, $"%{term}%"));
        }

        var total = await query.CountAsync(ct);
        var products = await query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (products, total);
    }

    public Task<StoreProduct?> FindActiveBySlugAsync(string slug, CancellationToken ct) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.Active, ct);
}
