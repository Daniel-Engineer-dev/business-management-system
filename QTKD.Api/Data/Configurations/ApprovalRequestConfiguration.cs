using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QTKD.Api.Models;

namespace QTKD.Api.Data.Configurations;

public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("approval_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(request => request.SubmittedBy).HasMaxLength(100).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(request => new { request.ContractId, request.IdempotencyKey }).IsUnique();
        builder.HasIndex(request => request.ContractId).IsUnique();

        builder.HasOne(request => request.Contract)
            .WithMany(contract => contract.ApprovalRequests)
            .HasForeignKey(request => request.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
