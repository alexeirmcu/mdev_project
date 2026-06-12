using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Infrastructure.Configurations;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.CityCode).IsRequired();
        builder.HasIndex(c => c.CityCode).IsUnique();

        builder.Property(c => c.IsAllowed).IsRequired().HasDefaultValue(true);

        builder.Property(c => c.CityName).IsRequired();
    }
}
