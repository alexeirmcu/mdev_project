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

        builder.HasIndex(p => p.ProviderReferenceId).IsUnique();
        builder.Property(p => p.ProviderReferenceId).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Provider).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.CityId).IsRequired();

        builder.HasOne(p => p.City)
            .WithMany(c => c.Places)
            .HasForeignKey(p => p.CityId);

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

        builder.OwnsMany(p => p.Attributes, attr =>
        {
            attr.WithOwner().HasForeignKey("PlaceId");
            attr.ToTable("PlaceAttributes");
            attr.Property<long>("Id");
            attr.HasKey("Id");
            attr.Property(a => a.Provider).IsRequired().HasMaxLength(100);
            attr.Property(a => a.Key).IsRequired().HasMaxLength(100);
            attr.Property(a => a.Value).IsRequired().HasMaxLength(500);
            attr.HasIndex("PlaceId", "Value");
        });

        builder.Property(p => p.TypicalDurationMinutes).HasDefaultValue(60);
        builder.Property(p => p.IsIndoor).HasDefaultValue(false);
        builder.Property(p => p.IsFamilyFriendly).HasDefaultValue(true);
        builder.Property(p => p.IsAutoUpdateEnabled).HasDefaultValue(true);
    }
}
