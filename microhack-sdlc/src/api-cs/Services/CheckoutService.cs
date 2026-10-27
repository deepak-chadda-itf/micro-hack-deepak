using OctoSupply.Api.Models;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Services;

public interface ICheckoutCartReader
{
    Task<Cart> FindAsync(Guid cartId);
}

public interface ICheckoutRepository
{
    Task<bool> BranchExistsAsync(int branchId);
    Task<Order> CreateOrderAndClearCartAsync(
        Guid cartId,
        int branchId,
        string customerName,
        Cart cart,
        PaymentResult payment);
}

public sealed class CheckoutService(
    ICheckoutCartReader cartReader,
    ICheckoutRepository checkoutRepository,
    IPaymentProvider paymentProvider)
{
    private readonly ICheckoutCartReader _cartReader = cartReader;
    private readonly ICheckoutRepository _checkoutRepository = checkoutRepository;
    private readonly IPaymentProvider _paymentProvider = paymentProvider;

    public async Task<CheckoutResponse> CheckoutAsync(Guid cartId, CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new ValidationException("customerName is required");
        }
        if (request.BranchId <= 0)
        {
            throw new ValidationException("branchId must be greater than zero");
        }
        if (string.IsNullOrWhiteSpace(request.PaymentMethodId))
        {
            throw new ValidationException("paymentMethodId is required");
        }

        var cart = await _cartReader.FindAsync(cartId);
        if (cart.Items.Count == 0 || cart.Total <= 0)
        {
            throw new ValidationException("Cart is empty");
        }
        if (!await _checkoutRepository.BranchExistsAsync(request.BranchId))
        {
            throw new NotFoundException("Branch", request.BranchId);
        }

        var payment = await _paymentProvider.ChargeAsync(cart.Total, request.PaymentMethodId, cancellationToken);
        var order = await _checkoutRepository.CreateOrderAndClearCartAsync(
            cartId,
            request.BranchId,
            request.CustomerName.Trim(),
            cart,
            payment);

        return new CheckoutResponse(order.OrderId, order.Status, cart.Total, payment.Provider);
    }
}