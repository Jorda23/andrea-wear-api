using AndreaWear.Entities;
using AndreaWearApi.Features.Admin.Repositories;
using AndreaWearApi.Features.Admin.Responses;
using AndreaWearApi.Features.Store.Responses;

namespace AndreaWearApi.Features.Admin.Services;

public sealed class AdminDashboardService(IAdminDashboardRepository dashboard) : IAdminDashboardService
{
    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken ct)
    {
        var start = DateTime.UtcNow.Date.AddDays(-6);
        var data = await dashboard.GetDashboardDataAsync(start, start.AddDays(7), ct);
        return new AdminDashboardResponse(
            data.TotalRevenue,
            data.OrderCount,
            data.PendingOrderCount,
            data.CustomerCount,
            data.ProductCount,
            data.ItemsSold,
            data.Revenue.Select(day => new DailyRevenueResponse(day.Date, day.Amount, day.Orders)).ToArray(),
            data.BestSellers.Select(item => new BestSellerResponse(item.ProductName, item.QuantitySold, item.Revenue)).ToArray(),
            data.RecentOrders.Select(ToResponse).ToArray(),
            data.ProductCount,
            data.ActiveProductCount,
            data.OrderCount,
            data.PendingOrderCount,
            data.CustomerCount);
    }

    private static OrderResponse ToResponse(StoreOrder order) => new(
        order.Id, order.OrderNumber, order.CustomerId, order.ShippingName, order.ShippingEmail,
        order.ShippingPhone, order.ShippingAddress, order.ShippingCity, order.ShippingPostalCode,
        order.ShippingCountry, order.Subtotal, order.ShippingCost, order.Total, order.Status, order.CreatedAt);
}
