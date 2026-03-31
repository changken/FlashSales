using FlashSales.Api.Models;

namespace FlashSales.Api.Services;

public interface ICampaignService
{
    Task<List<CampaignWithProduct>> ListAsync(CancellationToken ct = default);
    Task<CampaignWithProduct?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
