using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Infrastructure.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.Property(t => t.TripId).IsRequired();
        builder.HasIndex(t => t.TripId).IsUnique();

        builder.Property(t => t.TripCode).IsRequired().HasMaxLength(20);
        builder.HasIndex(t => t.TripCode).IsUnique();

        builder.Property(t => t.CityId).IsRequired();

        builder.HasOne(t => t.City)
            .WithMany()
            .HasForeignKey(t => t.CityId);

        builder.Property(t => t.StartDate).IsRequired();
        builder.Property(t => t.EndDate).IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();

        builder.Property(t => t.OwnerUserId).IsRequired().HasMaxLength(100);
        builder.HasIndex(t => t.OwnerUserId);

        builder.OwnsOne(t => t.BaseHotel, hotel =>
        {
            hotel.Property(h => h.Name).HasColumnName("HotelName").IsRequired().HasMaxLength(200);
            hotel.Property(h => h.Latitude).HasColumnName("HotelLatitude").IsRequired();
            hotel.Property(h => h.Longitude).HasColumnName("HotelLongitude").IsRequired();
        });

        builder.OwnsOne(t => t.Travelers, travelers =>
        {
            travelers.Property(tv => tv.Adults).HasColumnName("TravelersAdults").HasDefaultValue(2);
            travelers.Property(tv => tv.Children).HasColumnName("TravelersChildren").HasDefaultValue(0);
            travelers.Property(tv => tv.Infants).HasColumnName("TravelersInfants").HasDefaultValue(0);
        });

        builder.OwnsOne(t => t.Preferences, prefs =>
        {
            prefs.Property(p => p.CarAvailable).HasColumnName("PrefCarAvailable").HasDefaultValue(false);
            prefs.Property(p => p.MaxWalkingMinutes).HasColumnName("PrefMaxWalkingMinutes").HasDefaultValue(30);
            prefs.Property(p => p.WeatherAwareEnabled).HasColumnName("PrefWeatherAwareEnabled").HasDefaultValue(true);
            prefs.Property(p => p.ReturnToHotelStrategy)
                .HasColumnName("PrefReturnToHotelStrategy")
                .HasConversion<string>()
                .HasDefaultValue(ReturnToHotelStrategy.Always);
            prefs.Property(p => p.Interests)
                .HasColumnType("text[]")
                .HasDefaultValueSql("ARRAY[]::text[]");
            prefs.Property(p => p.AllowMustSeeOvertime)
                .HasColumnName("PrefAllowMustSeeOvertime")
                .HasDefaultValue(false);
        });

        builder.OwnsMany(t => t.OriginalMustSees, mustSee =>
        {
            mustSee.WithOwner().HasForeignKey("TripId");
            mustSee.Property<long>("Id");
            mustSee.HasKey("Id");
            mustSee.Property(m => m.PlaceId).IsRequired();
            mustSee.Property(m => m.Priority).HasConversion<string>().IsRequired();
            mustSee.Property(m => m.PinnedDayIndex);
            mustSee.Property(m => m.PinnedBlock).HasConversion<string>();
            mustSee.Property(m => m.ForceIncludeDespiteWeather).HasColumnName("ForceIncludeDespiteWeather").HasDefaultValue(false);
            mustSee.ToTable("TripMustSees");
        });

        // DayPlan is owned by Trip; BlockTimeline is owned by DayPlan but stored in its own BlockTimelines table
        builder.OwnsMany(t => t.Days, day =>
        {
            day.WithOwner().HasForeignKey("TripId");
            day.Property<long>("Id");
            day.HasKey("Id");
            day.Property(d => d.IsStale).HasColumnName("IsStale").HasDefaultValue(false);
            day.Property(d => d.WeatherLastUpdatedAt).HasColumnName("WeatherLastUpdatedAt");

            // BlockTimeline as OwnsMany → mapped to independent BlockTimelines table with FK DayPlanId
            day.OwnsMany(d => d.Blocks, block =>
            {
                block.WithOwner().HasForeignKey("DayPlanId");
                block.Property<long>("Id");
                block.HasKey("Id");
                block.Property(b => b.BlockType).HasConversion<string>().IsRequired();
                block.HasIndex("DayPlanId", "BlockType").IsUnique();

                block.OwnsOne(b => b.TransitFromHotel);
                block.OwnsOne(b => b.TransitToHotel);
                block.OwnsOne(b => b.InterBlockTransit);

                // Single Activities table consolidating all 3 block types
                block.OwnsMany(b => b.Activities, a =>
                {
                    a.ToTable("Activities");
                    a.WithOwner().HasForeignKey("BlockTimelineId");
                    a.Property<long>("Id");
                    a.HasKey("Id");
                    a.Property(ac => ac.OvertimeAlert).HasColumnName("OvertimeAlert").HasDefaultValue(false);
                    a.OwnsOne(ac => ac.TransitToNext);
                    a.OwnsOne(ac => ac.Location, loc =>
                    {
                        loc.Property(l => l.Latitude).HasColumnName("Latitude");
                        loc.Property(l => l.Longitude).HasColumnName("Longitude");
                    });
                });
            });
        });

        builder.Metadata.FindNavigation(nameof(Trip.Days))?.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Trip.OriginalMustSees))?.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
