using FlashSales.Api.Models;

namespace FlashSales.Api.Repositories;

public interface IOrderRepository
{
    Task<FlashOrder?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task<FlashOrder> CreateOrderAsync(Guid campaignId, Guid userId, int qty, string? idempotencyKey, CancellationToken ct = default);
}
