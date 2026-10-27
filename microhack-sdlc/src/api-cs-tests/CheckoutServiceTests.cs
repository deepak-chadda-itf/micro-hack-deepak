using OctoSupply.Api.Models;
using OctoSupply.Api.Services;
using OctoSupply.Api.Utils;
using Xunit;

namespace OctoSupply.Api.Tests;

public sealed class CheckoutServiceTests
{
    private static readonly Guid CartId = Guid.Parse("d1b332f2-d678-4316-8a72-d3297c808e99");

    [Fact]
    public async Task CheckoutAfterApprovedPayment_CreatesConfirmedOrder()
    {
        var cart = CreateCart();
        var repository = new StubCheckoutRepository(branchExists: true);
        var paymentProvider = new StubPaymentProvider();
        var service = CreateService(cart, repository, paymentProvider);

        var result = await service.CheckoutAsync(CartId, new CheckoutRequest
        {
            BranchId = 2,
            CustomerName = "  Casey Customer  ",
            PaymentMethodId = "demo-success"
        });

        Assert.Equal(321, result.OrderId);
        Assert.Equal("confirmed", result.Status);
        Assert.Equal(194.98m, result.Total);
        Assert.Equal(194.98m, paymentProvider.ChargedAmount);
        Assert.Equal("Casey Customer", repository.CustomerName);
        Assert.True(repository.OrderCreated);
    }

    [Fact]
    public async Task CheckoutAfterDeclinedPayment_LeavesCartAndDoesNotCreateOrder()
    {
        var cart = CreateCart();
        var repository = new StubCheckoutRepository(branchExists: true);
        var paymentProvider = new StubPaymentProvider(new PaymentException("Card declined"));
        var service = CreateService(cart, repository, paymentProvider);

        var exception = await Assert.ThrowsAsync<PaymentException>(() => service.CheckoutAsync(CartId, new CheckoutRequest
        {
            BranchId = 2,
            CustomerName = "Casey Customer",
            PaymentMethodId = "demo-decline"
        }));

        Assert.Contains("declined", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(repository.OrderCreated);
        Assert.Single(cart.Items);
    }

    [Fact]
    public async Task CheckoutWithEmptyCart_RejectsBeforeCharging()
    {
        var repository = new StubCheckoutRepository(branchExists: true);
        var paymentProvider = new StubPaymentProvider();
        var emptyCart = new Cart(CartId, [], 0, 0);
        var service = CreateService(emptyCart, repository, paymentProvider);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CheckoutAsync(CartId, new CheckoutRequest
        {
            BranchId = 2,
            CustomerName = "Casey Customer",
            PaymentMethodId = "demo-success"
        }));

        Assert.Contains("Cart is empty", exception.Message, StringComparison.Ordinal);
        Assert.Null(paymentProvider.ChargedAmount);
        Assert.False(repository.OrderCreated);
    }

    [Fact]
    public async Task CheckoutWithUnknownBranch_RejectsBeforeCharging()
    {
        var repository = new StubCheckoutRepository(branchExists: false);
        var paymentProvider = new StubPaymentProvider();
        var service = CreateService(CreateCart(), repository, paymentProvider);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => service.CheckoutAsync(CartId, new CheckoutRequest
        {
            BranchId = 999,
            CustomerName = "Casey Customer",
            PaymentMethodId = "demo-success"
        }));

        Assert.Contains("Branch with ID 999 not found", exception.Message, StringComparison.Ordinal);
        Assert.Null(paymentProvider.ChargedAmount);
        Assert.False(repository.OrderCreated);
    }

    private static CheckoutService CreateService(
        Cart cart,
        StubCheckoutRepository repository,
        StubPaymentProvider paymentProvider) =>
        new(new StubCartReader(cart), repository, paymentProvider);

    private static Cart CreateCart() => new(
        CartId,
        [new CartItem(1, "SmartFeeder One", "feeder.png", 2, 97.49m, 194.98m)],
        2,
        194.98m);

    private sealed class StubCartReader(Cart cart) : ICheckoutCartReader
    {
        public Task<Cart> FindAsync(Guid cartId) => Task.FromResult(cart);
    }

    private sealed class StubCheckoutRepository(bool branchExists) : ICheckoutRepository
    {
        public bool OrderCreated { get; private set; }
        public string? CustomerName { get; private set; }

        public Task<bool> BranchExistsAsync(int branchId) => Task.FromResult(branchExists);

        public Task<Order> CreateOrderAndClearCartAsync(
            Guid cartId,
            int branchId,
            string customerName,
            Cart cart,
            PaymentResult payment)
        {
            OrderCreated = true;
            CustomerName = customerName;
            return Task.FromResult(new Order(321, branchId, "2026-01-01T00:00:00Z", customerName, null, "confirmed"));
        }
    }

    private sealed class StubPaymentProvider(Exception? exception = null) : IPaymentProvider
    {
        public decimal? ChargedAmount { get; private set; }

        public Task<PaymentResult> ChargeAsync(decimal amount, string paymentMethodId, CancellationToken cancellationToken = default)
        {
            ChargedAmount = amount;
            return exception is null
                ? Task.FromResult(new PaymentResult("test", "payment-ref"))
                : Task.FromException<PaymentResult>(exception);
        }
    }
}