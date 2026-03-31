using FlashSales.Api.Models;
using FlashSales.Api.Repositories;
using FlashSales.Api.Services;
using NSubstitute;

namespace FlashSales.Tests;

public class OrderServiceTests
{
    private readonly IOrderRepository _repo = Substitute.For<IOrderRepository>();
    private readonly OrderService _svc;

    private static readonly FlashOrder _existingOrder = new()
    {
        Id        = Guid.NewGuid(),
        Status    = OrderStatus.Confirmed,
        UnitPrice = 29900,
        Qty       = 1,
        Subtotal  = 29900,
        CreatedAt = DateTime.UtcNow
    };

    public OrderServiceTests() => _svc = new OrderService(_repo);

    [Fact]
    public async Task CreateOrder_WithExistingIdempotencyKey_ReturnsExistingOrder_WithoutCallingCreate()
    {
        _repo.GetByIdempotencyKeyAsync("key-1", default).Returns(_existingOrder);

        var result = await _svc.CreateOrderAsync(Guid.NewGuid(), Guid.NewGuid(), 1, "key-1");

        Assert.Equal(_existingOrder.Id, result.Id);
        await _repo.DidNotReceive().CreateOrderAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrder_WithNewIdempotencyKey_CallsRepoCreate()
    {
        var newOrder = new FlashOrder { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        _repo.GetByIdempotencyKeyAsync("key-new", default).Returns((FlashOrder?)null);
        _repo.CreateOrderAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), "key-new", Arg.Any<CancellationToken>())
             .Returns(newOrder);

        var result = await _svc.CreateOrderAsync(Guid.NewGuid(), Guid.NewGuid(), 1, "key-new");

        Assert.Equal(newOrder.Id, result.Id);
    }

    [Fact]
    public async Task CreateOrder_WithNullIdempotencyKey_SkipsIdempotencyCheck()
    {
        var newOrder = new FlashOrder { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        _repo.CreateOrderAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), null, Arg.Any<CancellationToken>())
             .Returns(newOrder);

        await _svc.CreateOrderAsync(Guid.NewGuid(), Guid.NewGuid(), 1, null);

        await _repo.DidNotReceive().GetByIdempotencyKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
