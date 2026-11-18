using AndreaWear.Entities;
using AndreaWearApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AndreaWearApi.Features.Admin.Repositories;

public sealed class AdminDashboardRepository(ApplicationDbContext db) : IAdminDashboardRepository
{
    private static readonly string[] RevenueStatuses = ["confirmed", "shipped", "delivered"];

    public async Task<AdminDashboardData> GetDashboardDataAsync(
        DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking();
        var revenueOrders = orders.Where(o => RevenueStatuses.Contains(o.Status));
        var revenueInPeriod = revenueOrders.Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtc);

        var dailyRows = await revenueInPeriod
            .GroupBy(o => o.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Amount = group.Sum(o => o.Subtotal), Count = group.Count() })
            .ToListAsync(ct);
        var daily = dailyRows.ToDictionary(
            row => DateOnly.FromDateTime(row.Date),
            row => new DailyAdminRevenue(DateOnly.FromDateTime(row.Date), row.Amount, row.Count));

        var bestSellerRows = await db.OrderItems.AsNoTracking()
            .Where(item => item.Order != null && item.Order.Status != "cancelled")
            .GroupBy(item => item.ProductName)
            .Select(group => new
            {
                ProductName = group.Key,
                QuantitySold = group.Sum(item => item.Quantity),
                Revenue = group.Sum(item => item.UnitPrice * item.Quantity)
            })
            .OrderByDescending(item => item.QuantitySold)
            .Take(5)
            .ToListAsync(ct);

        var recentOrders = await orders.OrderByDescending(order => order.CreatedAt)
            .Take(8).ToListAsync(ct);
        var completedItems = await db.OrderItems.AsNoTracking()
            .Where(item => item.Order != null && RevenueStatuses.Contains(item.Order.Status))
            .SumAsync(item => (int?)item.Quantity, ct) ?? 0;

        return new AdminDashboardData(
            await db.Products.CountAsync(ct),
            await db.Products.CountAsync(product => product.Active, ct),
            await orders.CountAsync(ct),
            await orders.CountAsync(order => order.Status == "pending", ct),
            await db.Customers.CountAsync(ct),
            await revenueOrders.SumAsync(order => (decimal?)order.Subtotal, ct) ?? 0m,
            completedItems,
            recentOrders,
            Enumerable.Range(0, 7)
                .Select(offset => DateOnly.FromDateTime(startUtc.AddDays(offset)))
                .Select(date => daily.GetValueOrDefault(date, new DailyAdminRevenue(date, 0m, 0)))
                .ToArray(),
            bestSellerRows.Select(item => new AdminBestSeller(
                item.ProductName, item.QuantitySold, item.Revenue)).ToArray());
    }
}
