namespace QTKD.Api.Models;

public sealed class PriceItem
{
    public Guid Id { get; set; }
    public Guid PriceListId { get; set; }
    public required string ServiceCode { get; set; }
    public decimal UnitPrice { get; set; }

    public PriceList PriceList { get; set; } = null!;
}
