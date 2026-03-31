using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlashSales.Api.Models;
using FlashSales.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace FlashSales.Tests;

/// <summary>
/// Integration tests for POST /api/v1/orders covering:
///   - snake_case JSON deserialization
///   - malformed JSON → 400
///   - per-user rate limit (11th request → 429)
///   - different users / campaigns do not share rate-limit buckets
/// </summary>
public class RateLimitFilterTests : IClassFixture<RateLimitFilterTests.AppFactory>
{
    // ── Shared factory ────────────────────────────────────────────────────────

    public class AppFactory : WebApplicationFactory<Program>
    {
        // Counters keyed by Redis key, backed by a simple in-memory dictionary.
        // Simulates INCR + EXPIRE without a real Redis instance.
        private readonly Dictionary<string, long> _counters = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace IOrderService with a stub that always succeeds
                var orderSvc = Substitute.For<IOrderService>();
                orderSvc
                    .CreateOrderAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
                        Arg.Any<string?>(), Arg.Any<CancellationToken>())
                    .Returns(new FlashOrder
                    {
                        Id = Guid.NewGuid(),
                        Status = OrderStatus.Confirmed,
                        UnitPrice = 29900,
                        Qty = 1,
                        Subtotal = 29900,
                        CreatedAt = DateTime.UtcNow
                    });

                services.AddSingleton(orderSvc);

                // Replace IConnectionMultiplexer with an in-memory fake
                var db = Substitute.For<IDatabase>();
                db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
                    .Returns(ci =>
                    {
                        var key = ci.Arg<RedisKey>().ToString();
                        lock (_counters)
                        {
                            _counters.TryGetValue(key, out var cur);
                            _counters[key] = cur + 1;
                            return Task.FromResult(_counters[key]);
                        }
                    });
                db.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(),
                        Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(true));

                var mux = Substitute.For<IConnectionMultiplexer>();
                mux.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);

                services.AddSingleton(mux);
            });
        }

        /// <summary>Reset all counters between tests that need a clean slate.</summary>
        public void ResetCounters()
        {
            lock (_counters) _counters.Clear();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private readonly AppFactory _factory;
    private readonly HttpClient _client;

    private static readonly Guid _campaignId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public RateLimitFilterTests(AppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent OrderJson(Guid? userId = null, Guid? campaignId = null) =>
        new(JsonSerializer.Serialize(new
        {
            campaign_id = (campaignId ?? _campaignId).ToString(),
            user_id = (userId ?? Guid.NewGuid()).ToString(),
            qty = 1,
            idempotency_key = Guid.NewGuid().ToString()
        }), Encoding.UTF8, "application/json");

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_SnakeCaseJson_Returns201()
    {
        _factory.ResetCounters();
        var resp = await _client.PostAsync("/api/v1/orders", OrderJson());
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Post_MalformedJson_Returns400()
    {
        var resp = await _client.PostAsync("/api/v1/orders",
            new StringContent("{ not valid json }", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidGuidUserId_Returns400()
    {
        var body = new StringContent(
            """{"campaign_id":"00000000-0000-0000-0000-000000000002","user_id":"not-a-guid","qty":1}""",
            Encoding.UTF8, "application/json");
        var resp = await _client.PostAsync("/api/v1/orders", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_SameUser_11thRequest_Returns429()
    {
        _factory.ResetCounters();
        var userId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            var r = await _client.PostAsync("/api/v1/orders", OrderJson(userId));
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        var limited = await _client.PostAsync("/api/v1/orders", OrderJson(userId));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("60", limited.Headers.GetValues("Retry-After").First());
    }

    [Fact]
    public async Task Post_DifferentUsers_DoNotShareBucket()
    {
        _factory.ResetCounters();

        // Exhaust user A's bucket
        var userA = Guid.NewGuid();
        for (var i = 0; i < 11; i++)
            await _client.PostAsync("/api/v1/orders", OrderJson(userA));

        // User B should still get through
        var userB = Guid.NewGuid();
        var resp = await _client.PostAsync("/api/v1/orders", OrderJson(userB));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Post_DifferentCampaigns_DoNotShareBucket()
    {
        _factory.ResetCounters();
        var campaignA = Guid.NewGuid();
        var campaignB = Guid.NewGuid();

        // Both campaigns should succeed independently (well within per-campaign limit)
        var respA = await _client.PostAsync("/api/v1/orders", OrderJson(campaignId: campaignA));
        var respB = await _client.PostAsync("/api/v1/orders", OrderJson(campaignId: campaignB));

        Assert.Equal(HttpStatusCode.Created, respA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, respB.StatusCode);
    }
}
