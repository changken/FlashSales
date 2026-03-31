namespace FlashSales.Api.Dtos;

public class CreateOrderResponse
{
    public string OrderId { get; set; } = string.Empty;
    public short Status { get; set; }
    public decimal UnitPrice { get; set; }
    public int Qty { get; set; }
    public decimal Subtotal { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
