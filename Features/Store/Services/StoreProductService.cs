using AndreaWear.Entities;
using AndreaWearApi.Features.Store.Repositories;
using AndreaWearApi.Features.Store.Responses;

namespace AndreaWearApi.Features.Store.Services;

public sealed class StoreProductService(IStoreProductRepository products) : IStoreProductService
{
    public async Task<PageResponse<ProductResponse>> ListActiveAsync(
        string? category, bool? featured, string? search, int page, int pageSize, CancellationToken ct)
    {
        var (rows, total) = await products.ListActiveAsync(category, featured, search, page, pageSize, ct);
        return new PageResponse<ProductResponse>(rows.Select(ToResponse).ToArray(), page, pageSize, total);
    }

    public async Task<ProductResponse?> FindActiveBySlugAsync(string slug, CancellationToken ct)
    {
        var product = await products.FindActiveBySlugAsync(slug, ct);
        return product is null ? null : ToResponse(product);
    }

    private static ProductResponse ToResponse(StoreProduct product) => new(
        product.Id, product.Name, product.Slug, product.Description, product.Price, product.SalePrice,
        product.Category, product.Colors.ToArray(), product.Sizes.ToArray(), product.Stock,
        product.ImageUrl, product.Featured, product.Active, product.CreatedAt);
}
