using FlashSales.Api.Dtos;
using FlashSales.Api.Infrastructure;
using FlashSales.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlashSales.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _svc;

    public OrdersController(IOrderService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(req.CampaignId, out var campaignId))
            return BadRequest(new { error = "invalid campaign_id" });

        if (!Guid.TryParse(req.UserId, out var userId))
            return BadRequest(new { error = "invalid user_id" });

        try
        {
            var order = await _svc.CreateOrderAsync(campaignId, userId, req.Qty, req.IdempotencyKey, ct);
            return CreatedAtAction(nameof(Create), new { id = order.Id }, new CreateOrderResponse
            {
                OrderId = order.Id.ToString(),
                Status = (short)order.Status,
                UnitPrice = order.UnitPrice,
                Qty = order.Qty,
                Subtotal = order.Subtotal,
                CreatedAt = order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.ffffff zzz")
            });
        }
        catch (OutOfStockException)
        {
            return Conflict(new ErrorResponse
            {
                ErrorCode = "OUT_OF_STOCK",
                Message = "Sorry, the campaign items are sold out or the campaign is not active."
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse { Message = "internal server error" });
        }
    }
}
