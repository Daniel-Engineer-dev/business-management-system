using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Contracts;
using QTKD.Api.Data;
using QTKD.Api.Models;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/price-lists")]
public sealed class PriceListsController(QtkdDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await PriceListQuery(dbContext.PriceLists.AsNoTracking()
                .OrderByDescending(item => item.EffectiveDate))
            .ToListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var priceList = await PriceListQuery(dbContext.PriceLists.AsNoTracking()
                .Where(item => item.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return priceList is null ? NotFound() : Ok(priceList);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePriceListRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Tên bảng giá là bắt buộc.");
            return ValidationProblem(ModelState);
        }

        if (request.EffectiveDate == default)
        {
            ModelState.AddModelError(nameof(request.EffectiveDate), "Ngày hiệu lực là bắt buộc.");
            return ValidationProblem(ModelState);
        }

        var priceList = new PriceList
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            EffectiveDate = request.EffectiveDate
        };
        dbContext.PriceLists.Add(priceList);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = priceList.Id }, priceList);
    }

    [HttpGet("{id:guid}/items")]
    public async Task<IActionResult> GetItems(Guid id, CancellationToken cancellationToken)
    {
        if (!await dbContext.PriceLists.AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();

        return Ok(await dbContext.PriceItems.AsNoTracking()
            .Where(item => item.PriceListId == id)
            .OrderBy(item => item.ServiceCode)
            .Select(item => new { item.Id, item.PriceListId, item.ServiceCode, item.UnitPrice })
            .ToListAsync(cancellationToken));
    }

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(
        Guid id,
        CreatePriceItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.PriceLists.AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();

        if (string.IsNullOrWhiteSpace(request.ServiceCode))
            ModelState.AddModelError(nameof(request.ServiceCode), "Mã dịch vụ là bắt buộc.");
        if (request.UnitPrice < 0)
            ModelState.AddModelError(nameof(request.UnitPrice), "Đơn giá không được âm.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var serviceCode = request.ServiceCode.Trim().ToUpperInvariant();
        if (await dbContext.PriceItems.AnyAsync(
                item => item.PriceListId == id && item.ServiceCode == serviceCode, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Mã dịch vụ đã tồn tại trong bảng giá."
            });
        }

        var priceItem = new PriceItem
        {
            Id = Guid.NewGuid(),
            PriceListId = id,
            ServiceCode = serviceCode,
            UnitPrice = request.UnitPrice
        };
        dbContext.PriceItems.Add(priceItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetItems), new { id }, new
        {
            priceItem.Id, priceItem.PriceListId, priceItem.ServiceCode, priceItem.UnitPrice
        });
    }

    private static IQueryable<PriceListResponse> PriceListQuery(IQueryable<PriceList> priceLists) => priceLists
        .Select(priceList => new PriceListResponse(
            priceList.Id,
            priceList.Name,
            priceList.EffectiveDate,
            priceList.Items.OrderBy(item => item.ServiceCode)
                .Select(item => new PriceItemResponse(item.Id, item.ServiceCode, item.UnitPrice))));

    private sealed record PriceListResponse(
        Guid Id,
        string Name,
        DateOnly EffectiveDate,
        IEnumerable<PriceItemResponse> Items);

    private sealed record PriceItemResponse(Guid Id, string ServiceCode, decimal UnitPrice);
}
