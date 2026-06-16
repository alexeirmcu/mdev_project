using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Infrastructure.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.OwnsOne(t => t.BaseHotel, hotel =>
        {
            hotel.Property(h => h.Name).HasColumnName("HotelName");
            hotel.Property(h => h.Latitude).HasColumnName("HotelLatitude");
            hotel.Property(h => h.Longitude).HasColumnName("HotelLongitude");
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

        builder.HasMany(t => t.SelectedPlaces)
            .WithMany()
            .UsingEntity(j => j.ToTable("TripPlaces"));

        builder.Metadata.FindNavigation(nameof(Trip.Days))?.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
