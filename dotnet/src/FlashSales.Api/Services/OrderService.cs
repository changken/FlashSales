using FlashSales.Api.Models;
using FlashSales.Api.Repositories;

namespace FlashSales.Api.Services;

public class OrderService(IOrderRepository repo) : IOrderService
{
    public async Task<FlashOrder> CreateOrderAsync(Guid campaignId, Guid userId, int qty, string? idempotencyKey, CancellationToken ct = default)
    {
        // Idempotency check: return existing order if key was already used.
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await repo.GetByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing is not null)
                return existing;
        }

        return await repo.CreateOrderAsync(campaignId, userId, qty, idempotencyKey, ct);
    }
}
