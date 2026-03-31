using Dapper;
using FlashSales.Api.Infrastructure;
using FlashSales.Api.Models;

namespace FlashSales.Api.Repositories;

public class CampaignRepository(DbConnectionFactory factory) : ICampaignRepository
{
    private const string SelectColumns = """
        c.id, c.product_id, c.name, c.sale_price,
        c.total_qty, c.remaining_qty, c.start_at, c.end_at, c.status,
        c.created_by, c.updated_by, c.deleted_by,
        c.created_at, c.updated_at, c.deleted_at,
        p.name AS product_name, p.price AS orig_price
        """;

    public async Task<List<CampaignWithProduct>> ListAsync(CancellationToken ct = default)
    {
        await using var conn = await factory.CreateConnectionAsync(ct);
        var result = await conn.QueryAsync<CampaignWithProduct>($"""
            SELECT {SelectColumns}
            FROM campaign c
            JOIN product p ON p.id = c.product_id
            WHERE c.deleted_at IS NULL
            ORDER BY c.created_at DESC
            """);
        return result.AsList();
    }

    public async Task<CampaignWithProduct?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await factory.CreateConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<CampaignWithProduct>($"""
            SELECT {SelectColumns}
            FROM campaign c
            JOIN product p ON p.id = c.product_id
            WHERE c.id = @Id AND c.deleted_at IS NULL
            """, new { Id = id });
    }
}
