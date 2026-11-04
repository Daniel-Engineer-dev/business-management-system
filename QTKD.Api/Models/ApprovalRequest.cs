namespace QTKD.Api.Models;

public sealed class ApprovalRequest
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public ApprovalRequestStatus Status { get; set; }

    public Contract Contract { get; set; } = null!;
}

public enum ApprovalRequestStatus
{
    Pending
}
