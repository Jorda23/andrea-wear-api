using AndreaWear.Entities;

namespace AndreaWearApi.Features.Admin.Repositories;

public interface IAdminDashboardRepository
{
    Task<AdminDashboardData> GetDashboardDataAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct);
}

public sealed record AdminDashboardData(
    int ProductCount,
    int ActiveProductCount,
    int OrderCount,
    int PendingOrderCount,
    int CustomerCount,
    decimal TotalRevenue,
    int ItemsSold,
    IReadOnlyList<StoreOrder> RecentOrders,
    IReadOnlyList<DailyAdminRevenue> Revenue,
    IReadOnlyList<AdminBestSeller> BestSellers);

public sealed record DailyAdminRevenue(DateOnly Date, decimal Amount, int Orders);

public sealed record AdminBestSeller(string ProductName, int QuantitySold, decimal Revenue);
