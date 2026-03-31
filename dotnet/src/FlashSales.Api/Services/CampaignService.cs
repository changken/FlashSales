using FlashSales.Api.Models;
using FlashSales.Api.Repositories;

namespace FlashSales.Api.Services;

public class CampaignService(ICampaignRepository repo) : ICampaignService
{
    public Task<List<CampaignWithProduct>> ListAsync(CancellationToken ct = default)
        => repo.ListAsync(ct);

    public Task<CampaignWithProduct?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => repo.GetByIdAsync(id, ct);
}
