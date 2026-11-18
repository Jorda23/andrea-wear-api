namespace AndreaWearApi.Features.Admin.Requests;

public sealed class ProductWriteRequest
{
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public decimal? SalePrice { get; init; }
    public string? Category { get; init; }
    public string[]? Colors { get; init; }
    public string[]? Sizes { get; init; }
    public int Stock { get; init; }
    public string? ImageUrl { get; init; }
    public bool Featured { get; init; }
    public bool Active { get; init; } = true;
}

public sealed record SetProductActiveRequest(bool Active);
public sealed record UpdateOrderStatusRequest(string? Status);
public sealed record AdminLoginRequest(string? Code);

