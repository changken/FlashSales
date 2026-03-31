using Dapper;
using FlashSales.Api.Infrastructure;
using FlashSales.Api.Repositories;
using Npgsql;

namespace FlashSales.Tests;

/// <summary>
/// Concurrency test mirroring the Go version's 500-goroutine test.
/// Requires a running PostgreSQL instance with the FlashSales schema.
/// Set DATABASE_URL env var before running: e.g.
///   DATABASE_URL="Host=localhost;Port=5432;Database=flashsales;Username=flashsales;Password=flashsales" dotnet test
/// </summary>
public class OrderConcurrencyTests
{
    private const string CampaignId = "00000000-0000-0000-0000-000000000002";
    private const int Stock = 100;
    private const int VirtualUsers = 500;

    private static NpgsqlDataSource? _dataSource;

    private static NpgsqlDataSource GetDataSource()
    {
        if (_dataSource is not null) return _dataSource;

        var connStr = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=flashsales;Username=flashsales;Password=flashsales;Maximum Pool Size=200";

        // Ensure pool is large enough for 500 concurrent connections
        if (!connStr.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
            connStr += ";Maximum Pool Size=200";

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        _dataSource = NpgsqlDataSource.Create(connStr);
        return _dataSource;
    }

    [Fact]
    public async Task CreateOrder_500ConcurrentRequests_Exactly100Succeed()
    {
        var dataSource = GetDataSource();
        var factory = new DbConnectionFactory(dataSource);

        // Reset campaign inventory and clear orders
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await conn.ExecuteAsync(
                "UPDATE campaign SET remaining_qty = @stock WHERE id = @id::uuid",
                new { stock = Stock, id = CampaignId });
            await conn.ExecuteAsync(
                "DELETE FROM flash_order WHERE campaign_id = @id::uuid",
                new { id = CampaignId });
        }

        var repo = new OrderRepository(factory);
        var successCount = 0;
        var failCount = 0;

        // Launch 500 concurrent tasks — equivalent to Go's 500 goroutines + sync.WaitGroup
        var tasks = Enumerable.Range(0, VirtualUsers).Select(async i =>
        {
            var userId = Guid.NewGuid();
            var key = $"test-key-{i}";
            try
            {
                await repo.CreateOrderAsync(Guid.Parse(CampaignId), userId, 1, key);
                Interlocked.Increment(ref successCount);
            }
            catch (OutOfStockException)
            {
                Interlocked.Increment(ref failCount);
            }
        });

        await Task.WhenAll(tasks);

        Assert.Equal(Stock, successCount);
        Assert.Equal(VirtualUsers - Stock, failCount);

        // Verify DB state — remaining_qty must be exactly 0 (no oversell)
        await using var verifyConn = await dataSource.OpenConnectionAsync();
        var remainingQty = await verifyConn.ExecuteScalarAsync<int>(
            "SELECT remaining_qty FROM campaign WHERE id = @id::uuid",
            new { id = CampaignId });
        Assert.Equal(0, remainingQty);

        // Confirm exactly 100 orders were created with status=1 (Confirmed)
        var orderCount = await verifyConn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM flash_order WHERE campaign_id = @id::uuid AND status = 1",
            new { id = CampaignId });
        Assert.Equal(Stock, orderCount);
    }
}
