namespace QTKD.Api.Models;

public sealed class ContractHistory
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public required string Action { get; set; }
    public required string PerformedBy { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? Details { get; set; }

    public Contract Contract { get; set; } = null!;
}
