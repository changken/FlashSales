using Prometheus;

namespace FlashSales.Api.Middleware;

public static class MetricsRegistry
{
    // Order result counter (labels: success / out_of_stock / error / rate_limited)
    public static readonly Counter OrderResultsTotal = Metrics.CreateCounter(
        "flashsales_order_results_total",
        "Total number of order requests by result",
        new CounterConfiguration { LabelNames = new[] { "result" } }
    );

    // 訂單處理延遲
    public static readonly Histogram OrderDuration = Metrics.CreateHistogram(
        "flashsales_order_duration_seconds",
        "Order processing duration in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10) // 1ms ~ 1s
        }
    );

    // 庫存剩餘量（需要定期更新）
    public static readonly Gauge CampaignRemainingQty = Metrics.CreateGauge(
        "flashsales_campaign_remaining_qty",
        "Remaining quantity for campaigns",
        new GaugeConfiguration { LabelNames = new[] { "campaign_id" } }
    );

    // 限流拒絕計數
    public static readonly Counter RateLimitRejectionsTotal = Metrics.CreateCounter(
        "flashsales_rate_limit_rejections_total",
        "Total number of rate limit rejections",
        new CounterConfiguration { LabelNames = new[] { "type" } } // user / campaign
    );
}
