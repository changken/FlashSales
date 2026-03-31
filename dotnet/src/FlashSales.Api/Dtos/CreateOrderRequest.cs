using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FlashSales.Api.Dtos;

public class CreateOrderRequest
{
    [Required]
    [JsonPropertyName("campaign_id")]
    public string CampaignId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "qty must be at least 1")]
    public int Qty { get; set; }

    [JsonPropertyName("idempotency_key")]
    public string? IdempotencyKey { get; set; }
}
