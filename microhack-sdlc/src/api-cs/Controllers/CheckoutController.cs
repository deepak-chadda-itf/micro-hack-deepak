using Microsoft.AspNetCore.Mvc;
using OctoSupply.Api.Models;
using OctoSupply.Api.Services;

namespace OctoSupply.Api.Controllers;

[ApiController]
[Route("api/checkout")]
public sealed class CheckoutController(
    CheckoutService checkoutService,
    PaymentProviderOptions paymentOptions) : ControllerBase
{
    private readonly CheckoutService _checkoutService = checkoutService;
    private readonly PaymentProviderOptions _paymentOptions = paymentOptions;

    [HttpGet("config")]
    public ActionResult<PaymentConfigurationResponse> GetConfiguration() =>
        Ok(new PaymentConfigurationResponse(
            _paymentOptions.Provider,
            string.Equals(_paymentOptions.Provider, "stripe", StringComparison.OrdinalIgnoreCase)
                ? _paymentOptions.PublishableKey
                : null));

    [HttpPost("{cartId:guid}")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        Guid cartId,
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _checkoutService.CheckoutAsync(cartId, request, cancellationToken);
        return Created($"/api/orders/{result.OrderId}", result);
    }
}