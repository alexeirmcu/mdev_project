using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Infrastructure.Configurations;

public class PlaceAttributeConfiguration : IEntityTypeConfiguration<PlaceAttribute>
{
    public void Configure(EntityTypeBuilder<PlaceAttribute> builder)
    {
        builder.ToTable("PlaceAttributes");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();

        builder.Property(a => a.Provider)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Value)
            .IsRequired()
            .HasMaxLength(500);

        // Note: the case-insensitive unique index is created via raw SQL
        // in the migration (PostgreSQL functional index with LOWER()).
        // EF Core does not generate functional indexes from Fluent API.
    }
}
