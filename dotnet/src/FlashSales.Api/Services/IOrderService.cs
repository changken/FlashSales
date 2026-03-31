using FlashSales.Api.Models;

namespace FlashSales.Api.Services;

public interface IOrderService
{
    Task<FlashOrder> CreateOrderAsync(Guid campaignId, Guid userId, int qty, string? idempotencyKey, CancellationToken ct = default);
}
