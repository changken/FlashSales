using StackExchange.Redis;

namespace FlashSales.Api.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RateLimitMiddleware> _logger;

    // 限流配置
    private const int PerUserLimit = 10;      // 每個 user 每分鐘最多 10 次請求
    private const int PerCampaignLimit = 100; // 每個 campaign 每秒最多 100 次請求

    public RateLimitMiddleware(RequestDelegate next, IConnectionMultiplexer redis, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _redis = redis;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 只對 POST /orders 限流
        if (context.Request.Method != "POST" || !context.Request.Path.StartsWithSegments("/api/v1/orders"))
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;

        var request = System.Text.Json.JsonSerializer.Deserialize<OrderRequest>(body);
        if (request == null)
        {
            await _next(context);
            return;
        }

        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 檢查 per-user 限流
        var userKey = $"ratelimit:user:{request.UserId}";
        var userCount = await db.StringIncrementAsync(userKey);
        if (userCount == 1)
            await db.KeyExpireAsync(userKey, TimeSpan.FromMinutes(1));

        if (userCount > PerUserLimit)
        {
            _logger.LogWarning("Rate limit exceeded for user {UserId}", request.UserId);
            MetricsRegistry.RateLimitRejectionsTotal.WithLabels("user").Inc();
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsJsonAsync(new { error_code = "RATE_LIMIT_EXCEEDED", message = "Too many requests. Please try again later." });
            return;
        }

        // 檢查 per-campaign 限流
        var campaignKey = $"ratelimit:campaign:{request.CampaignId}:{now}";
        var campaignCount = await db.StringIncrementAsync(campaignKey);
        if (campaignCount == 1)
            await db.KeyExpireAsync(campaignKey, TimeSpan.FromSeconds(1));

        if (campaignCount > PerCampaignLimit)
        {
            _logger.LogWarning("Rate limit exceeded for campaign {CampaignId}", request.CampaignId);
            MetricsRegistry.RateLimitRejectionsTotal.WithLabels("campaign").Inc();
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = "1";
            await context.Response.WriteAsJsonAsync(new { error_code = "RATE_LIMIT_EXCEEDED", message = "Campaign is under heavy load. Please try again." });
            return;
        }

        await _next(context);
    }

    private record OrderRequest(string CampaignId, string UserId);
}
