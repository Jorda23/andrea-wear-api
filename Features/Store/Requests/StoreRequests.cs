using System.Text.Json.Serialization;

namespace AndreaWearApi.Features.Store.Requests;

public sealed record CreateWishlistRequest(
    [property: JsonPropertyName("productId")] Guid ProductId);

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
    [JsonPropertyName("addressLine1")]
    public string? AddressLine1Camel { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }

    public string ResolvedAddressLine1 => AddressLine1Camel ?? AddressLine1 ?? string.Empty;
}

public sealed class CheckoutItemRequest
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; init; }
    public string? Size { get; init; }
    public string? Color { get; init; }
    public int Quantity { get; init; }
}
