using FlashSales.Api.Models;
using FlashSales.Api.Repositories;

namespace FlashSales.Api.Services;

public class ProductService(IProductRepository repo) : IProductService
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => repo.GetByIdAsync(id, ct);
}
