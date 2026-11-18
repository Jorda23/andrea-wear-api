using AndreaWearApi.Features.Store.Responses;

namespace AndreaWearApi.Features.Store.Services;

public interface IStoreProductService
{
    Task<PageResponse<ProductResponse>> ListActiveAsync(
        string? category, bool? featured, string? search, int page, int pageSize, CancellationToken ct);

    Task<ProductResponse?> FindActiveBySlugAsync(string slug, CancellationToken ct);
}
