using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Infrastructure.Background;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;
using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Configuration;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather.Configuration;
using SmartTripPlanner.Infrastructure.LLM;
using SmartTripPlanner.Infrastructure.Outbox;
using SmartTripPlanner.Infrastructure.Repositories;
using SmartTripPlanner.Infrastructure.Services;

namespace SmartTripPlanner.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PlannerDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services.RegisterInfrastructureServices();
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, NpgsqlDataSource dataSource)
    {
        services.AddDbContext<PlannerDbContext>(options =>
            options.UseNpgsql(dataSource));

        return services.RegisterInfrastructureServices();
    }

    private static IServiceCollection RegisterInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PlannerDbContext>());

        services.AddScoped<ICityRepository, CityRepository>();
        services.AddScoped<IPlaceRepository, PlaceRepository>();
        services.AddScoped<ITripRepository, TripRepository>();

        services.AddOptions<FoursquareApiOptions>()
            .BindConfiguration(FoursquareApiOptions.SectionName);

        services.AddHttpClient<IFoursquareApiClient, FoursquareApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FoursquareApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            client.DefaultRequestHeaders.Add("X-Places-Api-Version", options.ApiVersion);
        });

        services.AddScoped<IPlaceExternalService, FoursquarePlaceService>();

        services.AddScoped<ITransitCalculator, HaversineTransitCalculator>();

        services.AddOptions<OpenMeteoApiOptions>()
            .BindConfiguration(OpenMeteoApiOptions.SectionName);

        services.AddHttpClient<IWeatherApiClient, OpenMeteoApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenMeteoApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddScoped<IWeatherProvider, OpenMeteoWeatherProvider>();

        services.AddOptions<LlmApiOptions>()
            .BindConfiguration(LlmApiOptions.SectionName);

        services.AddOptions<LlmEnrichmentOptions>()
            .BindConfiguration(LlmEnrichmentOptions.SectionName);

        services.AddOptions<PromptTemplateOptions>()
            .BindConfiguration(PromptTemplateOptions.SectionName);

        services.AddSingleton<IPromptTemplateProvider, ConfigurationPromptTemplateProvider>();
        services.AddScoped<PlaceEnrichmentPromptBuilder>();
        services.AddScoped<ILlmClient, LlmClient>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<ILlmEnrichmentProcessor, LlmEnrichmentProcessor>();
        services.AddHostedService<LlmEnrichmentBackgroundService>();

        return services;
    }
}
