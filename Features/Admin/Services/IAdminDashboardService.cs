using AndreaWearApi.Features.Admin.Responses;

namespace AndreaWearApi.Features.Admin.Services;

public interface IAdminDashboardService
{
    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken ct);
}
