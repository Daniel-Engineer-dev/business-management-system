using System.ComponentModel.DataAnnotations;

namespace QTKD.Api.Contracts;

public sealed record CreateContractRequest(
    [Required, MaxLength(50)] string ContractNo,
    Guid CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    [MaxLength(100)] string? ServiceGroup,
    [MaxLength(2000)] string? ContentSummary,
    [MaxLength(500)] string? AttachmentReference);

public sealed record UpdateContractRequest(
    Guid CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    [MaxLength(100)] string? ServiceGroup,
    [MaxLength(2000)] string? ContentSummary,
    [MaxLength(500)] string? AttachmentReference);

public sealed record SubmitContractResponse(
    Guid ContractId,
    string Status,
    Guid ApprovalId,
    bool IdempotentReplay);
