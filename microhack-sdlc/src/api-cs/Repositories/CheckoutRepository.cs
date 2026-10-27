using System.Globalization;
using Microsoft.Data.Sqlite;
using OctoSupply.Api.Data;
using OctoSupply.Api.Models;
using OctoSupply.Api.Services;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Repositories;

public sealed class CheckoutRepository(SqliteConnectionFactory connectionFactory) : ICheckoutRepository
{
    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory;

    public async Task<bool> BranchExistsAsync(int branchId)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM branches WHERE branch_id = $branchId);";
        command.Parameters.AddWithValue("$branchId", branchId);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    public async Task<Order> CreateOrderAndClearCartAsync(
        Guid cartId,
        int branchId,
        string customerName,
        Cart cart,
        PaymentResult payment)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var orderDate = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var description = $"Paid with {payment.Provider}; reference {payment.Reference}";
        await using var orderCommand = connection.CreateCommand();
        orderCommand.Transaction = (SqliteTransaction)transaction;
        orderCommand.CommandText = """
            INSERT INTO orders (branch_id, order_date, name, description, status)
            VALUES ($branchId, $orderDate, $name, $description, 'confirmed');
            SELECT last_insert_rowid();
            """;
        orderCommand.Parameters.AddWithValue("$branchId", branchId);
        orderCommand.Parameters.AddWithValue("$orderDate", orderDate);
        orderCommand.Parameters.AddWithValue("$name", customerName);
        orderCommand.Parameters.AddWithValue("$description", description);
        var orderId = Convert.ToInt32(await orderCommand.ExecuteScalarAsync());

        foreach (var item in cart.Items)
        {
            await using var detailCommand = connection.CreateCommand();
            detailCommand.Transaction = (SqliteTransaction)transaction;
            detailCommand.CommandText = """
                INSERT INTO order_details (order_id, product_id, quantity, unit_price)
                VALUES ($orderId, $productId, $quantity, $unitPrice);
                """;
            detailCommand.Parameters.AddWithValue("$orderId", orderId);
            detailCommand.Parameters.AddWithValue("$productId", item.ProductId);
            detailCommand.Parameters.AddWithValue("$quantity", item.Quantity);
            detailCommand.Parameters.AddWithValue("$unitPrice", item.UnitPrice);
            await detailCommand.ExecuteNonQueryAsync();
        }

        await using var clearCartCommand = connection.CreateCommand();
        clearCartCommand.Transaction = (SqliteTransaction)transaction;
        clearCartCommand.CommandText = "DELETE FROM cart_items WHERE cart_id = $cartId;";
        clearCartCommand.Parameters.AddWithValue("$cartId", cartId.ToString("D"));
        await clearCartCommand.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
        return new Order(orderId, branchId, orderDate, customerName, description, "confirmed");
    }
}