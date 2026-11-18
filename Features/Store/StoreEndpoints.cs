namespace AndreaWearApi.Features.Store;

public static class StoreEndpoints
{
    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Products");
        products.MapGet("", StoreHandlers.ListProductsAsync);
        products.MapGet("/{slug}", StoreHandlers.GetProductAsync);

        var wishlist = app.MapGroup("/api/wishlist").WithTags("Wishlist");
        wishlist.MapGet("", StoreHandlers.GetWishlistAsync);
        wishlist.MapPost("/items", StoreHandlers.AddWishlistItemAsync);
        wishlist.MapDelete("/items/{productId:guid}", StoreHandlers.DeleteWishlistItemAsync);

        var orders = app.MapGroup("/api/orders").WithTags("Orders");
        orders.MapPost("", StoreHandlers.CreateOrderAsync);
        orders.MapGet("/track/{orderNumber}", StoreHandlers.TrackOrderAsync);

        return app;
    }
}
