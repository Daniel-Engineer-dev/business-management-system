namespace QTKD.Api.Models;

public sealed class PriceList
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateOnly EffectiveDate { get; set; }

    public ICollection<PriceItem> Items { get; set; } = [];
}
