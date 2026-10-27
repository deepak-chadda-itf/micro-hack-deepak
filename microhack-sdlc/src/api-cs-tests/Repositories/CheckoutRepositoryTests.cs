using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OctoSupply.Api.Data;
using OctoSupply.Api.Models;
using OctoSupply.Api.Repositories;
using OctoSupply.Api.Services;
using Xunit;

namespace OctoSupply.Api.Tests.Repositories;

[CollectionDefinition("SQLite database tests", DisableParallelization = true)]
public sealed class SqliteDatabaseTestCollection;

[Collection("SQLite database tests")]
public sealed class CheckoutRepositoryTests
{
    private static readonly Guid CartId = Guid.Parse("d1b332f2-d678-4316-8a72-d3297c808e99");

    [Fact]
    public async Task CreateOrderAndClearCart_PersistsOrderDetailsAndEmptiesCart()
    {
        var (dbPath, previousDbPath, factory) = await CreateDatabaseAsync();
        try
        {
            var repository = new CheckoutRepository(factory);
            var cart = CreateCart();

            Assert.True(await repository.BranchExistsAsync(2));
            Assert.False(await repository.BranchExistsAsync(999));
            var order = await repository.CreateOrderAndClearCartAsync(
                CartId,
                2,
                "Casey Customer",
                cart,
                new PaymentResult("demo", "test-reference"));

            Assert.Equal("confirmed", order.Status);
            Assert.Equal("Casey Customer", order.Name);
            await using var connection = await factory.OpenConnectionAsync();
            Assert.Equal(1, await ReadCountAsync(connection, "SELECT COUNT(*) FROM orders WHERE order_id = $id AND status = 'confirmed';", order.OrderId));
            Assert.Equal(1, await ReadCountAsync(connection, "SELECT COUNT(*) FROM order_details WHERE order_id = $id AND product_id = 7 AND quantity = 2 AND unit_price = 12.5;", order.OrderId));
            Assert.Equal(0, await ReadCountAsync(connection, "SELECT COUNT(*) FROM cart_items WHERE cart_id = $cartId;", CartId.ToString("D")));
        }
        finally
        {
            await DeleteDatabaseAsync(dbPath, previousDbPath);
        }
    }

    [Fact]
    public async Task CreateOrderAndClearCart_WhenDetailInsertFails_RollsBackAndPreservesCart()
    {
        var (dbPath, previousDbPath, factory) = await CreateDatabaseAsync(rejectDetails: true);
        try
        {
            var repository = new CheckoutRepository(factory);

            await Assert.ThrowsAsync<SqliteException>(() => repository.CreateOrderAndClearCartAsync(
                CartId,
                2,
                "Casey Customer",
                CreateCart(),
                new PaymentResult("demo", "test-reference")));

            await using var connection = await factory.OpenConnectionAsync();
            Assert.Equal(0, await ReadCountAsync(connection, "SELECT COUNT(*) FROM orders;"));
            Assert.Equal(0, await ReadCountAsync(connection, "SELECT COUNT(*) FROM order_details;"));
            Assert.Equal(1, await ReadCountAsync(connection, "SELECT COUNT(*) FROM cart_items WHERE cart_id = $cartId;", CartId.ToString("D")));
        }
        finally
        {
            await DeleteDatabaseAsync(dbPath, previousDbPath);
        }
    }

    private static async Task<(string DbPath, string? PreviousDbPath, SqliteConnectionFactory Factory)> CreateDatabaseAsync(bool rejectDetails = false)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"octosupply-checkout-{Guid.NewGuid():N}.db");
        var previousDbPath = Environment.GetEnvironmentVariable("DB_FILE");
        Environment.SetEnvironmentVariable("DB_FILE", dbPath);
        var factory = new SqliteConnectionFactory(NullLogger<SqliteConnectionFactory>.Instance);

        await using var connection = await factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE branches (branch_id INTEGER PRIMARY KEY);
            CREATE TABLE products (product_id INTEGER PRIMARY KEY);
            CREATE TABLE orders (
                order_id INTEGER PRIMARY KEY AUTOINCREMENT,
                branch_id INTEGER NOT NULL REFERENCES branches(branch_id),
                order_date TEXT NOT NULL,
                name TEXT NOT NULL,
                description TEXT,
                status TEXT NOT NULL
            );
            CREATE TABLE order_details (
                order_detail_id INTEGER PRIMARY KEY AUTOINCREMENT,
                order_id INTEGER NOT NULL REFERENCES orders(order_id),
                product_id INTEGER NOT NULL REFERENCES products(product_id),
                quantity INTEGER NOT NULL,
                unit_price REAL NOT NULL
            );
            CREATE TABLE cart_items (
                cart_id TEXT NOT NULL,
                product_id INTEGER NOT NULL REFERENCES products(product_id),
                quantity INTEGER NOT NULL,
                PRIMARY KEY (cart_id, product_id)
            );
            INSERT INTO branches (branch_id) VALUES (2);
            INSERT INTO products (product_id) VALUES (7);
            INSERT INTO cart_items (cart_id, product_id, quantity) VALUES ('d1b332f2-d678-4316-8a72-d3297c808e99', 7, 2);
            """;
        if (rejectDetails)
        {
            command.CommandText += """

                CREATE TRIGGER reject_order_detail BEFORE INSERT ON order_details
                BEGIN
                    SELECT RAISE(FAIL, 'test detail insert failure');
                END;
                """;
        }
        await command.ExecuteNonQueryAsync();
        return (dbPath, previousDbPath, factory);
    }

    private static Cart CreateCart() => new(
        CartId,
        [new CartItem(7, "Test Product", null, 2, 12.5m, 25m)],
        2,
        25m);

    private static async Task<int> ReadCountAsync(SqliteConnection connection, string sql, object? parameter = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (sql.Contains("$id", StringComparison.Ordinal))
        {
            command.Parameters.AddWithValue("$id", parameter!);
        }
        else if (sql.Contains("$cartId", StringComparison.Ordinal))
        {
            command.Parameters.AddWithValue("$cartId", parameter!);
        }
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static Task DeleteDatabaseAsync(string dbPath, string? previousDbPath)
    {
        SqliteConnection.ClearAllPools();
        Environment.SetEnvironmentVariable("DB_FILE", previousDbPath);
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
        return Task.CompletedTask;
    }
}