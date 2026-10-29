namespace QTKD.Api.Models;

public sealed class Customer
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public CustomerStatus Status { get; set; }

    public ICollection<Contract> Contracts { get; set; } = [];
}

public enum CustomerStatus
{
    Inactive,
    Active
}
