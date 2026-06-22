using Microsoft.EntityFrameworkCore;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Infrastructure;

public class PlannerDbContext : DbContext, IUnitOfWork
{
    public DbSet<Trip> Trips { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Place> Places { get; set; }
    public DbSet<PlaceAttribute> PlaceAttributes { get; set; }
    public DbSet<PlacePlaceAttribute> PlacePlaceAttributes { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public PlannerDbContext(DbContextOptions<PlannerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlannerDbContext).Assembly);
    }

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        await base.SaveChangesAsync(cancellationToken);
        return true;
    }
}
