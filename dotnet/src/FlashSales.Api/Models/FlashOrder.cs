namespace FlashSales.Api.Models;

public class FlashOrder
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid UserId { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public OrderStatus Status { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
