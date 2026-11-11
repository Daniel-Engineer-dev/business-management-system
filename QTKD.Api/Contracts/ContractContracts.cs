using System.ComponentModel.DataAnnotations;
using QTKD.Api.Models;

namespace QTKD.Api.Contracts;

public sealed record CreateContractRequest(
    [Required, MaxLength(50)] string ContractNo,
    Guid CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    [MaxLength(100)] string? ServiceGroup,
    [MaxLength(2000)] string? ContentSummary,
    [MaxLength(500)] string? AttachmentReference,
    ContractStatus? Status = null);

public sealed record UpdateContractRequest(
    Guid CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    [MaxLength(100)] string? ServiceGroup,
    [MaxLength(2000)] string? ContentSummary,
    [MaxLength(500)] string? AttachmentReference,
    ContractStatus? Status = null);

public sealed record SubmitContractResponse(
    Guid ContractId,
    string Status,
    Guid ApprovalId,
    bool IdempotentReplay);
