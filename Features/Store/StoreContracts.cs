namespace AndreaWearApi.Features.Store;

public sealed record ProductResponse(
    Guid Id, string Name, string Slug, string Description, decimal Price,
    decimal? SalePrice, string Category, string[] Colors, string[] Sizes,
    int Stock, string? ImageUrl, bool Featured, bool Active, DateTime CreatedAt);

public sealed record OrderItemResponse(
    Guid Id, Guid OrderId, Guid? ProductId, string ProductName, string? Size,
    string? Color, decimal UnitPrice, int Quantity);

public sealed record OrderResponse(
    Guid Id, string OrderNumber, Guid? CustomerId, string ShippingName,
    string ShippingEmail, string? ShippingPhone, string ShippingAddress,
    string ShippingCity, string ShippingPostalCode, string ShippingCountry,
    decimal Subtotal, decimal ShippingCost, decimal Total, string Status,
    DateTime CreatedAt);

public sealed record OrderWithItemsResponse(
    Guid Id, string OrderNumber, Guid? CustomerId, string ShippingName,
    string ShippingEmail, string? ShippingPhone, string ShippingAddress,
    string ShippingCity, string ShippingPostalCode, string ShippingCountry,
    decimal Subtotal, decimal ShippingCost, decimal Total, string Status,
    DateTime CreatedAt, IReadOnlyList<OrderItemResponse> Items);

public sealed record TrackOrderResponse(
    string OrderNumber, string Status, DateTime CreatedAt, decimal Subtotal,
    decimal ShippingCost, decimal Total, IReadOnlyList<OrderItemResponse> Items);

public sealed record CustomerResponse(
    Guid Id, string Name, string Email, string? Phone, string AddressLine1,
    string City, string PostalCode, string Country, DateTime CreatedAt);

public sealed record PageResponse<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record CreateWishlistRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("productId")] Guid ProductId);

public sealed class CreateOrderRequest
{
    public CheckoutCustomerRequest? Customer { get; init; }
    public List<CheckoutItemRequest>? Items { get; init; }
}

public sealed class CheckoutCustomerRequest
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine1 { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("addressLine1")]
    public string? AddressLine1Camel { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }

    public string ResolvedAddressLine1 => AddressLine1Camel ?? AddressLine1 ?? string.Empty;
}

public sealed class CheckoutItemRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("productId")]
    public Guid ProductId { get; init; }
    public string? Size { get; init; }
    public string? Color { get; init; }
    public int Quantity { get; init; }
}

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

public sealed record AdminDashboardResponse(
    int ProductCount, int ActiveProductCount, int OrderCount,
    int PendingOrderCount, int CustomerCount,
    IReadOnlyList<OrderResponse> RecentOrders);
