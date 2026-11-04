using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Contracts;
using QTKD.Api.Data;
using QTKD.Api.Models;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(QtkdDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var customers = await dbContext.Customers.AsNoTracking()
            .OrderBy(customer => customer.Name)
            .Select(customer => new CustomerRow(customer.Id, customer.Name, customer.Status))
            .ToListAsync(cancellationToken);
        return Ok(customers.Select(customer => new
        {
            customer.Id, customer.Name, Status = customer.Status.ToString()
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new CustomerRow(item.Id, item.Name, item.Status))
            .SingleOrDefaultAsync(cancellationToken);
        return customer is null
            ? NotFound()
            : Ok(new { customer.Id, customer.Name, Status = customer.Status.ToString() });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Tên khách hàng là bắt buộc.");
            return ValidationProblem(ModelState);
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Status = request.Status
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, new
        {
            customer.Id, customer.Name, Status = customer.Status.ToString()
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Tên khách hàng là bắt buộc.");
            return ValidationProblem(ModelState);
        }

        var customer = await dbContext.Customers.FindAsync([id], cancellationToken);
        if (customer is null) return NotFound();

        customer.Name = request.Name.Trim();
        customer.Status = request.Status;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { customer.Id, customer.Name, Status = customer.Status.ToString() });
    }

    private sealed record CustomerRow(Guid Id, string Name, CustomerStatus Status);
}
