using FlashSales.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlashSales.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _svc;

    public ProductsController(IProductService svc) => _svc = svc;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var product = await _svc.GetByIdAsync(id, ct);
        return product is null
            ? NotFound(new { error = "product not found" })
            : Ok(product);
    }
}
