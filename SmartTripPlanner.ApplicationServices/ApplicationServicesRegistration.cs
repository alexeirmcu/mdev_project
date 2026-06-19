using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartTripPlanner.ApplicationServices.Behaviors;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Services;

namespace SmartTripPlanner.ApplicationServices;

public static class ApplicationServicesRegistration
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(ApplicationServicesRegistration).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<ITripCodeGenerator, TripCodeGenerator>();

        services.AddScoped<IItineraryGenerator, HeuristicItineraryGenerator>();
        services.AddScoped<ICandidateScorer, CandidateScorer>();

        // Phase collaborator services
        services.AddScoped<IPinnedMustSeePlacer, PinnedMustSeePlacer>();
        services.AddScoped<IUnpinnedMustSeePlacer, UnpinnedMustSeePlacer>();
        services.AddScoped<ICandidateFiller, CandidateFiller>();
        services.AddScoped<ITransitEnricher, TransitEnricher>();

        return services;
    }
}
