namespace FlashSales.Api.Dtos;

public class ErrorResponse
{
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
