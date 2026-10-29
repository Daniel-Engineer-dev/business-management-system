using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QTKD.Api.Models;

namespace QTKD.Api.Data.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts", table =>
            table.HasCheckConstraint("ck_contract_dates", "\"EndDate\" >= \"StartDate\""));
        builder.HasKey(contract => contract.Id);
        builder.Property(contract => contract.ContractNo).HasMaxLength(50).IsRequired();
        builder.HasIndex(contract => contract.ContractNo).IsUnique();
        builder.Property(contract => contract.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
    }
}
