using System.ComponentModel.DataAnnotations;

namespace QTKD.Api.Contracts;

public sealed record CreatePriceListRequest(
    [Required, MaxLength(200)] string Name,
    DateOnly EffectiveDate);

public sealed record CreatePriceItemRequest(
    [Required, MaxLength(50)] string ServiceCode,
    decimal UnitPrice);
