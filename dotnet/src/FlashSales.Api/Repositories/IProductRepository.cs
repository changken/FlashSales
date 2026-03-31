using FlashSales.Api.Models;

namespace FlashSales.Api.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
