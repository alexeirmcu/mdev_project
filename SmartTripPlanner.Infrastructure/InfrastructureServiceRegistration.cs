using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Infrastructure.Repositories;

namespace SmartTripPlanner.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PlannerDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PlannerDbContext>());

        services.AddScoped<IPlaceRepository, PlaceRepository>();

        return services;
    }
}
