namespace FlashSales.Api.Models;

public class Campaign
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public int TotalQty { get; set; }
    public int RemainingQty { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public CampaignStatus Status { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class CampaignWithProduct : Campaign
{
    public string ProductName { get; set; } = string.Empty;
    public decimal OrigPrice { get; set; }
}
