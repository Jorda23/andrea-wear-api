namespace AndreaWearApi.Features.Store.Responses;

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

public sealed record TrackOrderResponse(
    string OrderNumber, string Status, DateTime CreatedAt, decimal Subtotal,
    decimal ShippingCost, decimal Total, IReadOnlyList<OrderItemResponse> Items);

public sealed record PageResponse<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
