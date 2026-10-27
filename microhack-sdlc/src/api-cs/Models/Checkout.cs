namespace OctoSupply.Api.Models;

public sealed class CheckoutRequest
{
    public int BranchId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PaymentMethodId { get; set; } = string.Empty;
}

public sealed record CheckoutResponse(
    int OrderId,
    string Status,
    decimal Total,
    string PaymentProvider);

public sealed record PaymentConfigurationResponse(
    string Provider,
    string? PublishableKey);