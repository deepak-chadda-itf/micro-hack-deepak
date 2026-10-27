using Microsoft.Data.Sqlite;
using OctoSupply.Api.Data;
using OctoSupply.Api.Models;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Repositories;

public sealed class CartRepository(SqliteConnectionFactory connectionFactory)
{
    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory;

    public async Task<Cart> FindAsync(Guid cartId)
    {
        const string sql = """
            SELECT p.product_id, p.name, p.img_name, p.price, p.discount, ci.quantity
            FROM cart_items ci
            INNER JOIN products p ON p.product_id = ci.product_id
            WHERE ci.cart_id = $cartId
            ORDER BY p.product_id;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$cartId", cartId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<CartItem>();
        while (await reader.ReadAsync())
        {
            var price = Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("price")));
            var discount = Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("discount")));
            var quantity = reader.GetInt32(reader.GetOrdinal("quantity"));
            var unitPrice = Math.Round(price * (1m - discount), 2, MidpointRounding.AwayFromZero);
            var lineTotal = Math.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);

            items.Add(new CartItem(
                ProductId: reader.GetInt32(reader.GetOrdinal("product_id")),
                ProductName: reader.GetString(reader.GetOrdinal("name")),
                ImgName: reader["img_name"] as string,
                Quantity: quantity,
                UnitPrice: unitPrice,
                LineTotal: lineTotal));
        }

        return new Cart(cartId, items, items.Sum(item => item.Quantity), items.Sum(item => item.LineTotal));
    }

    public async Task<Cart> AddItemAsync(Guid cartId, int productId, int quantity)
    {
        if (productId <= 0)
        {
            throw new ValidationException("productId must be greater than zero");
        }
        if (quantity <= 0)
        {
            throw new ValidationException("quantity must be greater than zero");
        }

        const string sql = """
            INSERT INTO cart_items (cart_id, product_id, quantity)
            SELECT $cartId, product_id, $quantity
            FROM products
            WHERE product_id = $productId
            ON CONFLICT (cart_id, product_id)
            DO UPDATE SET quantity = cart_items.quantity + excluded.quantity;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$cartId", cartId.ToString("D"));
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$quantity", quantity);

        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new NotFoundException("Product", productId);
        }

        return await FindAsync(cartId);
    }

    public async Task<Cart> UpdateItemAsync(Guid cartId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException("quantity must be greater than zero");
        }

        const string sql = """
            UPDATE cart_items
            SET quantity = $quantity
            WHERE cart_id = $cartId AND product_id = $productId;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$cartId", cartId.ToString("D"));
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$quantity", quantity);

        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new NotFoundException("Cart item", productId);
        }

        return await FindAsync(cartId);
    }

    public async Task<Cart> RemoveItemAsync(Guid cartId, int productId)
    {
        const string sql = "DELETE FROM cart_items WHERE cart_id = $cartId AND product_id = $productId;";
        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$cartId", cartId.ToString("D"));
        command.Parameters.AddWithValue("$productId", productId);

        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new NotFoundException("Cart item", productId);
        }

        return await FindAsync(cartId);
    }
}