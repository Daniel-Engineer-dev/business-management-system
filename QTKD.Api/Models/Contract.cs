namespace QTKD.Api.Models;

public sealed class Contract
{
    public Guid Id { get; set; }
    public required string ContractNo { get; set; }
    public Guid CustomerId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public ContractStatus Status { get; set; }

    public Customer Customer { get; set; } = null!;
}

public enum ContractStatus
{
    Draft,
    PendingApproval,
    RevisionRequested,
    Rejected,
    Approved,
    Active
}
