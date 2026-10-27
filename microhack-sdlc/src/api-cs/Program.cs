using Azure.Monitor.OpenTelemetry.AspNetCore;
using OctoSupply.Api.Data;
using OctoSupply.Api.Repositories;
using OctoSupply.Api.Services;
using OctoSupply.Api.Utils;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<SqliteConnectionFactory>();
builder.Services.AddSingleton<DatabaseMigrator>();
var paymentOptions = new PaymentProviderOptions(
    builder.Configuration["PAYMENT_PROVIDER"]?.Trim().ToLowerInvariant() ?? "demo",
    builder.Configuration["PAYMENT_API_KEY"],
    builder.Configuration["PAYMENT_PUBLISHABLE_KEY"]);
builder.Services.AddSingleton(paymentOptions);
builder.Services.AddHttpClient<StripePaymentProvider>();
builder.Services.AddScoped<ICheckoutCartReader>(services => services.GetRequiredService<CartRepository>());
builder.Services.AddScoped<ICheckoutRepository, CheckoutRepository>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<IPaymentProvider>(services => paymentOptions.Provider switch
{
    "demo" => new DemoPaymentProvider(),
    "stripe" => services.GetRequiredService<StripePaymentProvider>(),
    _ => throw new InvalidOperationException($"Unsupported PAYMENT_PROVIDER '{paymentOptions.Provider}'. Use 'demo' or 'stripe'.")
});
builder.Services.AddScoped<BranchesRepository>();
builder.Services.AddScoped<CartRepository>();
builder.Services.AddScoped<DeliveriesRepository>();
builder.Services.AddScoped<HeadquartersRepository>();
builder.Services.AddScoped<OrderDetailDeliveriesRepository>();
builder.Services.AddScoped<OrderDetailsRepository>();
builder.Services.AddScoped<OrdersRepository>();
builder.Services.AddScoped<ProductsRepository>();
builder.Services.AddScoped<SuppliersRepository>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        var configuredOrigins = Environment.GetEnvironmentVariable("API_CORS_ORIGINS");
        if (!string.IsNullOrWhiteSpace(configuredOrigins))
        {
            var origins = configuredOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            return;
        }

        policy
            .SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
                origin.Contains(".app.github.dev", StringComparison.OrdinalIgnoreCase) ||
                origin.Contains(".azurecontainerapps.io", StringComparison.OrdinalIgnoreCase))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.MapOpenApi("/api-docs.json");
app.UseCors("ApiCors");
app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapGet("/", () => "Hello, world!");
// Liveness: the process is up and serving requests.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
// Readiness: the database must be reachable and migrated.
app.MapGet("/health/ready", async (SqliteConnectionFactory factory, ILogger<Program> logger) =>
{
    try
    {
        await using var connection = await factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM products";
        await command.ExecuteScalarAsync();
        return Results.Ok(new { status = "ready", database = "ok" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Readiness check failed: database unavailable");
        return Results.Json(new { status = "unready", database = "error" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapControllers();

var migrator = app.Services.GetRequiredService<DatabaseMigrator>();
await migrator.InitializeAsync(seedOnStartup: true);

app.Run();
