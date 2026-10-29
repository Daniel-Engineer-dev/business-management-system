using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Data;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public sealed class ContractsController(QtkdDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var contracts = await dbContext.Contracts
            .AsNoTracking()
            .OrderBy(contract => contract.ContractNo)
            .Select(contract => new
            {
                contract.Id,
                contract.ContractNo,
                contract.CustomerId,
                CustomerName = contract.Customer.Name,
                contract.StartDate,
                contract.EndDate,
                Status = contract.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return Ok(contracts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts
            .AsNoTracking()
            .Where(contract => contract.Id == id)
            .Select(contract => new
            {
                contract.Id,
                contract.ContractNo,
                contract.CustomerId,
                CustomerName = contract.Customer.Name,
                contract.StartDate,
                contract.EndDate,
                Status = contract.Status.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return contract is null ? NotFound() : Ok(contract);
    }
}
