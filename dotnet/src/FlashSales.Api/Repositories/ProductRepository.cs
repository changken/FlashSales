using Dapper;
using FlashSales.Api.Infrastructure;
using FlashSales.Api.Models;

namespace FlashSales.Api.Repositories;

public class ProductRepository(DbConnectionFactory factory) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await factory.CreateConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Product>("""
            SELECT id, name, description, price, status,
                   created_by, updated_by, deleted_by,
                   created_at, updated_at, deleted_at
            FROM product
            WHERE id = @Id AND deleted_at IS NULL AND status = 0
            """, new { Id = id });
    }
}
