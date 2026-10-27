using System.Net.Http.Headers;
using System.Text.Json;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Services;

public sealed record PaymentProviderOptions(
    string Provider,
    string? ApiKey,
    string? PublishableKey);

public sealed record PaymentResult(string Provider, string Reference);

public interface IPaymentProvider
{
    Task<PaymentResult> ChargeAsync(decimal amount, string paymentMethodId, CancellationToken cancellationToken = default);
}

public sealed class DemoPaymentProvider : IPaymentProvider
{
    public Task<PaymentResult> ChargeAsync(decimal amount, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ValidationException("Payment amount must be greater than zero");
        }

        return paymentMethodId switch
        {
            "demo-success" => Task.FromResult(new PaymentResult("demo", Guid.NewGuid().ToString("D"))),
            "demo-decline" => throw new PaymentException("The demo payment was declined. Choose the approved demo card to continue."),
            _ => throw new ValidationException("Select a valid demo payment method")
        };
    }
}

public sealed class StripePaymentProvider(HttpClient httpClient, PaymentProviderOptions options) : IPaymentProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly PaymentProviderOptions _options = options;

    public async Task<PaymentResult> ChargeAsync(decimal amount, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new PaymentException("Stripe is selected but PAYMENT_API_KEY is not configured.", StatusCodes.Status503ServiceUnavailable);
        }
        if (amount <= 0)
        {
            throw new ValidationException("Payment amount must be greater than zero");
        }
        if (string.IsNullOrWhiteSpace(paymentMethodId) || !paymentMethodId.StartsWith("pm_", StringComparison.Ordinal))
        {
            throw new ValidationException("A valid Stripe payment method is required");
        }

        var amountInCents = decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/payment_intents");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["amount"] = amountInCents.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["currency"] = "usd",
            ["payment_method"] = paymentMethodId,
            ["payment_method_types[0]"] = "card",
            ["confirm"] = "true"
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!response.IsSuccessStatusCode)
        {
            var message = root.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var errorMessage)
                ? errorMessage.GetString()
                : null;
            throw new PaymentException(message ?? "Stripe could not process this payment.");
        }

        var status = root.TryGetProperty("status", out var statusProperty) ? statusProperty.GetString() : null;
        if (!string.Equals(status, "succeeded", StringComparison.Ordinal))
        {
            throw new PaymentException("Payment was not completed. This card may require additional authentication or may have been declined.");
        }

        var reference = root.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new PaymentException("Stripe returned an invalid payment confirmation.", StatusCodes.Status502BadGateway);
        }

        return new PaymentResult("stripe", reference);
    }
}