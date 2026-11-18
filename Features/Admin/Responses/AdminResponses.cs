using AndreaWearApi.Features.Store.Responses;

namespace AndreaWearApi.Features.Admin.Responses;

public sealed record AdminDashboardResponse(
    decimal TotalRevenue,
    int TotalOrders,
    int AwaitingFulfillment,
    int Customers,
    int Products,
    int ItemsSold,
    IReadOnlyList<DailyRevenueResponse> Revenue,
    IReadOnlyList<BestSellerResponse> BestSellers,
    IReadOnlyList<OrderResponse> RecentOrders,
    int ProductCount,
    int ActiveProductCount,
    int OrderCount,
    int PendingOrderCount,
    int CustomerCount);

public sealed record DailyRevenueResponse(DateOnly Date, decimal Amount, int Orders);

public sealed record BestSellerResponse(
    string ProductName, int QuantitySold, decimal Revenue);

public sealed record CustomerResponse(
    Guid Id, string Name, string Email, string? Phone, string AddressLine1,
    string City, string PostalCode, string Country, DateTime CreatedAt);

public sealed record OrderWithItemsResponse(
    Guid Id, string OrderNumber, Guid? CustomerId, string ShippingName,
    string ShippingEmail, string? ShippingPhone, string ShippingAddress,
    string ShippingCity, string ShippingPostalCode, string ShippingCountry,
    decimal Subtotal, decimal ShippingCost, decimal Total, string Status,
    DateTime CreatedAt, IReadOnlyList<OrderItemResponse> Items);

