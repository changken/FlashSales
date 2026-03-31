using FlashSales.Api.Models;

namespace FlashSales.Api.Services;

public interface IProductService
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
