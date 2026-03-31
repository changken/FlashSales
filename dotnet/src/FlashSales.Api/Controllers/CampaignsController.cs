using FlashSales.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlashSales.Api.Controllers;

[ApiController]
[Route("api/v1/campaigns")]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _svc;

    public CampaignsController(ICampaignService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var campaigns = await _svc.ListAsync(ct);
        return Ok(campaigns);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var campaign = await _svc.GetByIdAsync(id, ct);
        return campaign is null
            ? NotFound(new { error = "campaign not found" })
            : Ok(campaign);
    }
}
