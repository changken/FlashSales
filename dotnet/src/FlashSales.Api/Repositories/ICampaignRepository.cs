using FlashSales.Api.Models;

namespace FlashSales.Api.Repositories;

public interface ICampaignRepository
{
    Task<List<CampaignWithProduct>> ListAsync(CancellationToken ct = default);
    Task<CampaignWithProduct?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
