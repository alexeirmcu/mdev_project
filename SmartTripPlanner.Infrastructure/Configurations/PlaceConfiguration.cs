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

        builder.HasMany(p => p.Attributes)
            .WithMany()
            .UsingEntity<PlacePlaceAttribute>(
                j => j.HasOne(pp => pp.PlaceAttribute)
                    .WithMany()
                    .HasForeignKey(pp => pp.PlaceAttributeId),
                j => j.HasOne(pp => pp.Place)
                    .WithMany()
                    .HasForeignKey(pp => pp.PlaceId),
                j =>
                {
                    j.ToTable("PlacePlaceAttributes");
                    j.HasKey(pp => new { pp.PlaceId, pp.PlaceAttributeId });
                });

        builder.Property(p => p.TypicalDurationMinutes).HasDefaultValue(60);
        builder.Property(p => p.IsIndoor).HasDefaultValue(false);
        builder.Property(p => p.IsFamilyFriendly).HasDefaultValue(true);
        builder.Property(p => p.IsAutoUpdateEnabled).HasDefaultValue(true);
        builder.Property(p => p.FamilyFriendlyScore).HasDefaultValue(3);
        builder.Property(p => p.Popularity).HasDefaultValue(0.5);
        builder.Property(p => p.IsEnriched).HasDefaultValue(false);
    }
}
