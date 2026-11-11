using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Contracts;
using QTKD.Api.Data;
using QTKD.Api.Models;
using QTKD.Api.Services;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public sealed class ContractsController(QtkdDbContext dbContext, ContractSubmissionService submissionService)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var contracts = await ContractDataQuery(dbContext.Contracts.AsNoTracking()
                .OrderBy(contract => contract.ContractNo))
            .ToListAsync(cancellationToken);
        return Ok(contracts.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var contract = await ContractDataQuery(dbContext.Contracts.AsNoTracking()
                .Where(item => item.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return contract is null ? NotFound() : Ok(ToResponse(contract));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateContractRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not null && request.Status != ContractStatus.Draft)
            return Conflict(new { code = "CONTRACT_STATE_INVALID", message = "Hợp đồng mới phải ở Draft (CTR-03)." });

        ValidateContractRequest(request.ContractNo, request.CustomerId, request.StartDate, request.EndDate);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!await IsValidCustomer(request.CustomerId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.CustomerId), "Customer phải tồn tại và ở trạng thái Active.");
            return ValidationProblem(ModelState);
        }

        var normalizedContractNo = request.ContractNo.Trim();
        if (await dbContext.Contracts.AnyAsync(item => item.ContractNo == normalizedContractNo, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Số hợp đồng đã tồn tại.",
                Detail = $"ContractNo '{normalizedContractNo}' không được trùng."
            });
        }

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNo = normalizedContractNo,
            CustomerId = request.CustomerId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ServiceGroup = Normalize(request.ServiceGroup),
            ContentSummary = Normalize(request.ContentSummary),
            AttachmentReference = Normalize(request.AttachmentReference),
            Status = ContractStatus.Draft
        };
        dbContext.Contracts.Add(contract);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = contract.Id }, new
        {
            contract.Id, contract.ContractNo, Status = contract.Status.ToString()
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateContractRequest request, CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts.FindAsync([id], cancellationToken);
        if (contract is null) return NotFound();

        if (!ContractRules.CanEdit(contract.Status))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "CTR-01: Không thể sửa hợp đồng ở trạng thái hiện tại.",
                Detail = $"Chỉ Draft hoặc RevisionRequested được phép sửa; trạng thái hiện tại là {contract.Status}."
            });
        }

        if (request.Status is not null && request.Status != contract.Status)
            return Conflict(new { code = "CONTRACT_STATE_INVALID", message = "Thay đổi trạng thái phải thông qua thao tác nghiệp vụ (CTR-03)." });

        ValidateContractRequest(contract.ContractNo, request.CustomerId, request.StartDate, request.EndDate);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!await IsValidCustomer(request.CustomerId, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.CustomerId), "Customer phải tồn tại và ở trạng thái Active.");
            return ValidationProblem(ModelState);
        }

        contract.CustomerId = request.CustomerId;
        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.ServiceGroup = Normalize(request.ServiceGroup);
        contract.ContentSummary = Normalize(request.ContentSummary);
        contract.AttachmentReference = Normalize(request.AttachmentReference);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { contract.Id, contract.ContractNo, Status = contract.Status.ToString() });
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-User-Role", out var role) ||
            !string.Equals(role.ToString(), "Sales", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Không có quyền gửi hợp đồng.",
                Detail = "Bản demo yêu cầu header X-User-Role: Sales."
            });
        }

        if (!Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
        {
            ModelState.AddModelError("Idempotency-Key", "Header Idempotency-Key là bắt buộc.");
            return ValidationProblem(ModelState);
        }

        var idempotencyKey = key.ToString().Trim();
        if (idempotencyKey.Length > 100)
        {
            ModelState.AddModelError("Idempotency-Key", "Idempotency-Key không được dài quá 100 ký tự.");
            return ValidationProblem(ModelState);
        }

        var submittedBy = Request.Headers.TryGetValue("X-User-Id", out var userId) &&
                          !string.IsNullOrWhiteSpace(userId)
            ? userId.ToString().Trim()
            : "demo-sales-user";

        var result = await submissionService.SubmitAsync(
            id, idempotencyKey, submittedBy[..Math.Min(submittedBy.Length, 100)], cancellationToken);

        return result.Kind switch
        {
            SubmitContractResultKind.Success => Ok(result.Response),
            SubmitContractResultKind.NotFound => NotFound(),
            SubmitContractResultKind.Invalid => BadRequest(new
            {
                code = result.ErrorCode, message = result.Message, unmetConditions = result.UnmetConditions
            }),
            SubmitContractResultKind.Conflict => Conflict(new
            {
                code = result.ErrorCode, message = result.Message
            }),
            _ => throw new InvalidOperationException("Kết quả submit không được hỗ trợ.")
        };
    }

    private static IQueryable<ContractData> ContractDataQuery(IQueryable<Contract> contracts) => contracts
        .Select(contract => new ContractData(
            contract.Id,
            contract.ContractNo,
            contract.CustomerId,
            contract.Customer.Name,
            contract.StartDate,
            contract.EndDate,
            contract.ServiceGroup,
            contract.ContentSummary,
            contract.AttachmentReference,
            contract.Status,
            contract.ApprovalRequests.Select(request => (Guid?)request.Id).FirstOrDefault()));

    private static ContractResponse ToResponse(ContractData contract) => new(
        contract.Id,
        contract.ContractNo,
        contract.CustomerId,
        contract.CustomerName,
        contract.StartDate,
        contract.EndDate,
        contract.ServiceGroup,
        contract.ContentSummary,
        contract.AttachmentReference,
        contract.Status.ToString(),
        contract.ApprovalId);

    private async Task<bool> IsValidCustomer(Guid customerId, CancellationToken cancellationToken) =>
        customerId != Guid.Empty && await dbContext.Customers.AnyAsync(
            customer => customer.Id == customerId && customer.Status == CustomerStatus.Active,
            cancellationToken);

    private void ValidateContractRequest(string contractNo, Guid customerId, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(contractNo))
            ModelState.AddModelError(nameof(contractNo), "Số hợp đồng là bắt buộc.");
        if (customerId == Guid.Empty)
            ModelState.AddModelError(nameof(customerId), "CustomerId là bắt buộc.");
        if (startDate == default || endDate == default || endDate < startDate)
            ModelState.AddModelError(nameof(endDate), "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ContractResponse(
        Guid Id,
        string ContractNo,
        Guid CustomerId,
        string CustomerName,
        DateOnly StartDate,
        DateOnly EndDate,
        string? ServiceGroup,
        string? ContentSummary,
        string? AttachmentReference,
        string Status,
        Guid? ApprovalId);

    private sealed record ContractData(
        Guid Id,
        string ContractNo,
        Guid CustomerId,
        string CustomerName,
        DateOnly StartDate,
        DateOnly EndDate,
        string? ServiceGroup,
        string? ContentSummary,
        string? AttachmentReference,
        ContractStatus Status,
        Guid? ApprovalId);
}
