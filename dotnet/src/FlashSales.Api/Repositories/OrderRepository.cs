using Dapper;
using FlashSales.Api.Infrastructure;
using FlashSales.Api.Models;
using Npgsql;

namespace FlashSales.Api.Repositories;

public class OrderRepository(DbConnectionFactory factory) : IOrderRepository
{
    public async Task<FlashOrder?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
    {
        await using var conn = await factory.CreateConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<FlashOrder>("""
            SELECT id, campaign_id, user_id, qty, unit_price, subtotal, status, created_at, updated_at
            FROM flash_order
            WHERE idempotency_key = @Key
            """, new { Key = key });
    }

    public async Task<FlashOrder> CreateOrderAsync(Guid campaignId, Guid userId, int qty, string? idempotencyKey, CancellationToken ct = default)
    {
        await using var conn = await factory.CreateConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Step 1: Atomic deduct — only succeeds if campaign is active, in-time, and has stock.
        await using (var deductCmd = new NpgsqlCommand("""
            UPDATE campaign
            SET remaining_qty = remaining_qty - @qty,
                updated_at    = NOW()
            WHERE id            = @campaignId
              AND status        = 1
              AND remaining_qty >= @qty
              AND start_at      <= NOW()
              AND end_at        >= NOW()
              AND deleted_at    IS NULL
            """, conn, tx))
        {
            deductCmd.Parameters.AddWithValue("qty", qty);
            deductCmd.Parameters.AddWithValue("campaignId", campaignId);

            var rowsAffected = await deductCmd.ExecuteNonQueryAsync(ct);
            if (rowsAffected == 0)
                throw new OutOfStockException();
        }

        // Step 2: Fetch sale_price snapshot from campaign (within same tx for consistency).
        decimal salePrice;
        await using (var priceCmd = new NpgsqlCommand(
            "SELECT sale_price FROM campaign WHERE id = @id", conn, tx))
        {
            priceCmd.Parameters.AddWithValue("id", campaignId);
            salePrice = (decimal)(await priceCmd.ExecuteScalarAsync(ct))!;
        }

        // Step 3: Insert order record with idempotency key.
        var orderId = Guid.NewGuid();
        var subtotal = salePrice * qty;
        await using (var insertCmd = new NpgsqlCommand("""
            INSERT INTO flash_order (id, campaign_id, user_id, qty, unit_price, subtotal, status, idempotency_key, created_at, updated_at)
            VALUES (@id, @campaignId, @userId, @qty, @unitPrice, @subtotal, @status, @idempotencyKey, NOW(), NOW())
            """, conn, tx))
        {
            insertCmd.Parameters.AddWithValue("id", orderId);
            insertCmd.Parameters.AddWithValue("campaignId", campaignId);
            insertCmd.Parameters.AddWithValue("userId", userId);
            insertCmd.Parameters.AddWithValue("qty", qty);
            insertCmd.Parameters.AddWithValue("unitPrice", salePrice);
            insertCmd.Parameters.AddWithValue("subtotal", subtotal);
            insertCmd.Parameters.AddWithValue("status", (short)OrderStatus.Confirmed);
            insertCmd.Parameters.AddWithValue("idempotencyKey", (object?)idempotencyKey ?? DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        // Re-fetch created order for accurate DB timestamp
        return (await conn.QuerySingleAsync<FlashOrder>("""
            SELECT id, campaign_id, user_id, qty, unit_price, subtotal, status, idempotency_key, created_at, updated_at
            FROM flash_order WHERE id = @Id
            """, new { Id = orderId }));
    }
}
