using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Infrastructure.Configurations;

public class PlaceConfiguration : IEntityTypeConfiguration<Place>
{
    public void Configure(EntityTypeBuilder<Place> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        builder.HasIndex(p => p.PlaceId).IsUnique();
        builder.Property(p => p.PlaceId).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.CityId).IsRequired().HasMaxLength(50);

        builder.OwnsOne(p => p.Location, loc =>
        {
            loc.Property(l => l.Latitude).HasColumnName("Location_Latitude");
            loc.Property(l => l.Longitude).HasColumnName("Location_Longitude");
        });

        builder.OwnsMany(p => p.OpeningHours, oh =>
        {
            oh.WithOwner().HasForeignKey("PlaceId");
            oh.ToTable("PlaceOpeningHours");
            oh.Property<long>("Id");
            oh.HasKey("Id");
            oh.Property(ohw => ohw.DayOfWeek).IsRequired();
            oh.Property(ohw => ohw.OpenMinutes).IsRequired();
            oh.Property(ohw => ohw.CloseMinutes).IsRequired();
        });

        builder.Property(p => p.TypicalDurationMinutes).HasDefaultValue(60);
        builder.Property(p => p.IsIndoor).HasDefaultValue(false);
        builder.Property(p => p.IsFamilyFriendly).HasDefaultValue(true);
    }
}
