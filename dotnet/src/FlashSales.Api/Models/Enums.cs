namespace FlashSales.Api.Models;

public enum ProductStatus : short
{
    Active = 0,
    Inactive = 1
}

public enum CampaignStatus : short
{
    Draft = 0,
    Active = 1,
    Ended = 2,
    Cancelled = 3
}

public enum OrderStatus : short
{
    Pending = 0,
    Confirmed = 1,
    Failed = 2,
    Cancelled = 3
}
