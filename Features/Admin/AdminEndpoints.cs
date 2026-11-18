namespace AndreaWearApi.Features.Admin;

using AndreaWearApi.Features.Admin.Requests;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/auth/login", AdminHandlers.LoginAdminAsync)
            .RequireRateLimiting("admin-login")
            .WithTags("Admin Auth");

        var admin = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization();
        admin.MapPost("/auth/logout", (Delegate)AdminHandlers.LogoutAdminAsync);
        admin.MapGet("/dashboard", AdminHandlers.GetDashboardAsync);
        admin.MapGet("/products", AdminHandlers.ListAdminProductsAsync);
        admin.MapPost("/products", AdminHandlers.CreateProductAsync);
        admin.MapPut("/products/{id:guid}", AdminHandlers.UpdateProductAsync);
        admin.MapDelete("/products/{id:guid}", AdminHandlers.DeleteProductAsync);
        admin.MapPatch("/products/{id:guid}/active", AdminHandlers.SetProductActiveAsync);
        admin.MapGet("/orders", AdminHandlers.ListAdminOrdersAsync);
        admin.MapPatch("/orders/{id:guid}/status", AdminHandlers.SetOrderStatusAsync);
        admin.MapGet("/customers", AdminHandlers.ListCustomersAsync);
        return app;
    }
}
