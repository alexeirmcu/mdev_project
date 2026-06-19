using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartTripPlanner.Infrastructure;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Connection string is read from environment variable or defaults to local development DB.
/// </summary>
public class PlannerDbContextFactory : IDesignTimeDbContextFactory<PlannerDbContext>
{
    public PlannerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlannerDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("SmartTripPlanner_ConnectionString")
            ?? "Host=localhost;Port=5432;Database=SmartTripPlanner;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new PlannerDbContext(optionsBuilder.Options);
    }
}
