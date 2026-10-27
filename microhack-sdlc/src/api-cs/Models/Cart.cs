namespace OctoSupply.Api.Models;

public sealed record CartItem(
    int ProductId,
    string ProductName,
    string? ImgName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record Cart(
    Guid CartId,
    IReadOnlyList<CartItem> Items,
    int ItemCount,
    decimal Total);

public sealed class AddCartItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public sealed class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}