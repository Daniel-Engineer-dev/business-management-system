using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QTKD.Api.Models;

namespace QTKD.Api.Data.Configurations;

public sealed class ContractHistoryConfiguration : IEntityTypeConfiguration<ContractHistory>
{
    public void Configure(EntityTypeBuilder<ContractHistory> builder)
    {
        builder.ToTable("contract_history");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Action).HasMaxLength(50).IsRequired();
        builder.Property(history => history.PerformedBy).HasMaxLength(100).IsRequired();
        builder.Property(history => history.Details).HasMaxLength(1000);
        builder.HasIndex(history => new { history.ContractId, history.OccurredAtUtc });

        builder.HasOne(history => history.Contract)
            .WithMany(contract => contract.History)
            .HasForeignKey(history => history.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
