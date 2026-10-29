using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QTKD.Api.Models;

namespace QTKD.Api.Data.Configurations;

public sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("price_lists");
        builder.HasKey(priceList => priceList.Id);
        builder.Property(priceList => priceList.Name).HasMaxLength(200).IsRequired();

        builder.HasMany(priceList => priceList.Items)
            .WithOne(item => item.PriceList)
            .HasForeignKey(item => item.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
