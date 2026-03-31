using Npgsql;

namespace FlashSales.Api.Infrastructure;

public class DbConnectionFactory(NpgsqlDataSource dataSource)
{
    public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        return await dataSource.OpenConnectionAsync(ct);
    }
}
