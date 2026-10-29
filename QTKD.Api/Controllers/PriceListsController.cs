using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Data;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/price-lists")]
public sealed class PriceListsController(QtkdDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var priceLists = await dbContext.PriceLists
            .AsNoTracking()
            .OrderByDescending(priceList => priceList.EffectiveDate)
            .Select(priceList => new
            {
                priceList.Id,
                priceList.Name,
                priceList.EffectiveDate,
                Items = priceList.Items
                    .OrderBy(item => item.ServiceCode)
                    .Select(item => new { item.Id, item.ServiceCode, item.UnitPrice })
            })
            .ToListAsync(cancellationToken);

        return Ok(priceLists);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var priceList = await dbContext.PriceLists
            .AsNoTracking()
            .Where(priceList => priceList.Id == id)
            .Select(priceList => new
            {
                priceList.Id,
                priceList.Name,
                priceList.EffectiveDate,
                Items = priceList.Items
                    .OrderBy(item => item.ServiceCode)
                    .Select(item => new { item.Id, item.ServiceCode, item.UnitPrice })
            })
            .SingleOrDefaultAsync(cancellationToken);

        return priceList is null ? NotFound() : Ok(priceList);
    }
}
