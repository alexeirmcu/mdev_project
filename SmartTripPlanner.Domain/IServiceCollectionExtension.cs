using Microsoft.Extensions.DependencyInjection;

namespace SmartTripPlanner.Domain;

public static class IServiceCollectionExtension
{
    public static IServiceCollection AddDomain(this IServiceCollection services)
    {
        // Domain layer owns the interface contracts.
        // Concrete implementations are registered by Infrastructure.
        return services;
    }
}
