using FlashSales.Api.Dtos;
using FlashSales.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StackExchange.Redis;

namespace FlashSales.Api.Filters;

public class RateLimitFilter : IAsyncActionFilter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RateLimitFilter> _logger;

    private const int PerUserLimit     = 10;   // per user per minute
    private const int PerCampaignLimit = 100;  // per campaign per second

    public RateLimitFilter(IConnectionMultiplexer redis, ILogger<RateLimitFilter> logger)
    {
        _redis  = redis;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments.Values.FirstOrDefault(v => v is CreateOrderRequest)
                is not CreateOrderRequest req)
        {
            await next();
            return;
        }

        if (!Guid.TryParse(req.UserId, out var userId) ||
            !Guid.TryParse(req.CampaignId, out var campaignId))
        {
            await next();
            return;
        }

        var db  = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Per-user limit (sliding window: 10 req / minute)
        var userKey   = $"ratelimit:user:{userId}";
        var userCount = await db.StringIncrementAsync(userKey);
        if (userCount == 1)
            await db.KeyExpireAsync(userKey, TimeSpan.FromMinutes(1));

        if (userCount > PerUserLimit)
        {
            _logger.LogWarning("Rate limit exceeded for user {UserId}", userId);
            MetricsRegistry.RateLimitRejectionsTotal.WithLabels("user").Inc();
            Reject(context, "60", "Too many requests. Please try again later.");
            return;
        }

        // Per-campaign limit (fixed window: 100 req / second)
        var campaignKey   = $"ratelimit:campaign:{campaignId}:{now}";
        var campaignCount = await db.StringIncrementAsync(campaignKey);
        if (campaignCount == 1)
            await db.KeyExpireAsync(campaignKey, TimeSpan.FromSeconds(1));

        if (campaignCount > PerCampaignLimit)
        {
            _logger.LogWarning("Rate limit exceeded for campaign {CampaignId}", campaignId);
            MetricsRegistry.RateLimitRejectionsTotal.WithLabels("campaign").Inc();
            Reject(context, "1", "Campaign is under heavy load. Please try again.");
            return;
        }

        await next();
    }

    private static void Reject(ActionExecutingContext context, string retryAfter, string message)
    {
        context.HttpContext.Response.Headers["Retry-After"] = retryAfter;
        context.Result = new ObjectResult(new { error_code = "RATE_LIMIT_EXCEEDED", message })
        {
            StatusCode = StatusCodes.Status429TooManyRequests
        };
    }
}
