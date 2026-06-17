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
        builder.Property(t => t.StartDate).IsRequired();
        builder.Property(t => t.EndDate).IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(TripStatus.CREATED);

        builder.Property(t => t.CreatedAt).IsRequired();

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
            mustSee.ToTable("TripMustSees");
        });

        builder.OwnsMany(t => t.Days, day =>
        {
            day.WithOwner().HasForeignKey("TripId");
            day.Property<long>("Id");
            day.HasKey("Id");

            day.OwnsOne(d => d.Morning, m => m.OwnsMany(b => b.Activities, a =>
            {
                a.ToTable("MorningActivities");
                a.WithOwner().HasForeignKey("DayPlanId");
                a.Property<long>("Id");
                a.HasKey("Id");
                a.OwnsOne(ac => ac.TransitToNext);
            }));
            day.OwnsOne(d => d.Afternoon, m => m.OwnsMany(b => b.Activities, a =>
            {
                a.ToTable("AfternoonActivities");
                a.WithOwner().HasForeignKey("DayPlanId");
                a.Property<long>("Id");
                a.HasKey("Id");
                a.OwnsOne(ac => ac.TransitToNext);
            }));
            day.OwnsOne(d => d.Evening, m => m.OwnsMany(b => b.Activities, a =>
            {
                a.ToTable("EveningActivities");
                a.WithOwner().HasForeignKey("DayPlanId");
                a.Property<long>("Id");
                a.HasKey("Id");
                a.OwnsOne(ac => ac.TransitToNext);
            }));
        });

        builder.Metadata.FindNavigation(nameof(Trip.Days))?.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Trip.OriginalMustSees))?.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
