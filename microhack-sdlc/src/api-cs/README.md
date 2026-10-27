# OctoSupply API (C#)

ASP.NET Core (.NET 10) port of the OctoSupply API.

## Run

```bash
dotnet restore
dotnet run
```

Default API URL: `http://localhost:3000`

## Environment

- `PORT` (default: `3000`)
- `DB_FILE` (default: `./data/app.db`)
- `API_CORS_ORIGINS` (comma-separated override; optional)

## Database

On startup, the API applies SQL migrations from:

- `../database/migrations/*.sql`

and seeds from:

- `../database/seed/*.sql`

## Checkout and Payments

Checkout uses the `PAYMENT_PROVIDER` environment variable. The default is `demo`, which does not
collect card details and provides approved and declined outcomes for local demonstration. The cart is
converted into a confirmed order, its order details are written, and the cart is cleared only after
payment succeeds.

To use Stripe test mode, configure the API process with environment variables before starting it:

```powershell
$env:PAYMENT_PROVIDER = "stripe"
$env:PAYMENT_API_KEY = "sk_test_..."
$env:PAYMENT_PUBLISHABLE_KEY = "pk_test_..."
dotnet run
```

Keep `PAYMENT_API_KEY` secret and out of source control. The API exposes only the publishable key to
the frontend through `GET /api/checkout/config`; Stripe tokenizes card details in the browser. The
checkout endpoint is `POST /api/checkout/{cartId}` and accepts `branchId`, `customerName`, and
`paymentMethodId`. This sample handles synchronous payment success and decline; it does not yet
implement 3-D Secure challenges or idempotent retry/reconciliation, so Stripe mode is for test use,
not production payment processing.

## OpenAPI

- OpenAPI JSON: `http://localhost:3000/api-docs.json`

## Tests

Run C# tests from the `src/` directory:

```bash
make test-cs
```
