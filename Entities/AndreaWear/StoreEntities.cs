namespace AndreaWear.Entities;

public sealed class StoreProduct
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string Category { get; set; } = "general";
    public List<string> Colors { get; set; } = [];
    public List<string> Sizes { get; set; } = [];
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }
    public bool Featured { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class StoreCustomer
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class StoreOrder
{
    public Guid Id { get; set; }
    public required string OrderNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public StoreCustomer? Customer { get; set; }
    public required string ShippingName { get; set; }
    public required string ShippingEmail { get; set; }
    public string? ShippingPhone { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string ShippingCountry { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public List<StoreOrderItem> Items { get; set; } = [];
}

public sealed class StoreOrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public StoreOrder? Order { get; set; }
    public Guid? ProductId { get; set; }
    public StoreProduct? Product { get; set; }
    public required string ProductName { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public sealed class StoreWishlistItem
{
    public Guid Id { get; set; }
    public required string VisitorToken { get; set; }
    public Guid ProductId { get; set; }
    public StoreProduct? Product { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class StoreSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
