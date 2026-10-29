using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QTKD.Api.Models;

namespace QTKD.Api.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
        builder.Property(customer => customer.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasMany(customer => customer.Contracts)
            .WithOne(contract => contract.Customer)
            .HasForeignKey(contract => contract.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
