using AndreaWear.Entities;

namespace AndreaWearApi.Features.Store.Repositories;

public interface IStoreProductRepository
{
    Task<(IReadOnlyList<StoreProduct> Products, int TotalCount)> ListActiveAsync(
        string? category, bool? featured, string? search, int page, int pageSize, CancellationToken ct);

    Task<StoreProduct?> FindActiveBySlugAsync(string slug, CancellationToken ct);
}
