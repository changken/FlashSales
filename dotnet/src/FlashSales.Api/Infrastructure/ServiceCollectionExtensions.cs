using FlashSales.Api.Repositories;
using FlashSales.Api.Services;
using Npgsql;

namespace FlashSales.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlashSalesServices(this IServiceCollection services, string connectionString)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);
        services.AddSingleton(dataSource);
        services.AddSingleton<DbConnectionFactory>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<IOrderService, OrderService>();

        return services;
    }
}
