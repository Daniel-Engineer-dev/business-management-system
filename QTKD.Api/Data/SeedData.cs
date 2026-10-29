using Microsoft.EntityFrameworkCore;
using QTKD.Api.Models;

namespace QTKD.Api.Data;

internal static class SeedData
{
    internal static readonly Guid CustomerMinhPhatId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid CustomerAnBinhId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    internal static readonly Guid ContractId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    internal static readonly Guid PriceListId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    internal static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasData(
            new Customer { Id = CustomerMinhPhatId, Name = "Công ty Minh Phát", Status = CustomerStatus.Active },
            new Customer { Id = CustomerAnBinhId, Name = "Công ty An Bình", Status = CustomerStatus.Active });

        modelBuilder.Entity<Contract>().HasData(new Contract
        {
            Id = ContractId,
            ContractNo = "HD-2026-018",
            CustomerId = CustomerMinhPhatId,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2027, 9, 30),
            Status = ContractStatus.Draft
        });

        modelBuilder.Entity<PriceList>().HasData(new PriceList
        {
            Id = PriceListId,
            Name = "Bảng giá dịch vụ cảng 2026",
            EffectiveDate = new DateOnly(2026, 1, 1)
        });

        modelBuilder.Entity<PriceItem>().HasData(
            new PriceItem
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
                PriceListId = PriceListId,
                ServiceCode = "PORT-LOAD",
                UnitPrice = 1_500_000m
            },
            new PriceItem
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000002"),
                PriceListId = PriceListId,
                ServiceCode = "PORT-STORAGE",
                UnitPrice = 250_000m
            });
    }
}
