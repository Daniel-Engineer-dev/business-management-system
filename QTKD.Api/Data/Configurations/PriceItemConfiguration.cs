using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QTKD.Api.Models;

namespace QTKD.Api.Data.Configurations;

public sealed class PriceItemConfiguration : IEntityTypeConfiguration<PriceItem>
{
    public void Configure(EntityTypeBuilder<PriceItem> builder)
    {
        builder.ToTable("price_items", table =>
            table.HasCheckConstraint("ck_price_item_unit_price", "\"UnitPrice\" >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ServiceCode).HasMaxLength(50).IsRequired();
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.HasIndex(item => new { item.PriceListId, item.ServiceCode }).IsUnique();
    }
}
