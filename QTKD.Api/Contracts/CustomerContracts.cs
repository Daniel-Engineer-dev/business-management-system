using System.ComponentModel.DataAnnotations;
using QTKD.Api.Models;

namespace QTKD.Api.Contracts;

public sealed record CreateCustomerRequest(
    [Required, MaxLength(200)] string Name,
    CustomerStatus Status = CustomerStatus.Active);

public sealed record UpdateCustomerRequest(
    [Required, MaxLength(200)] string Name,
    CustomerStatus Status);
