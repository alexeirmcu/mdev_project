namespace SmartTripPlanner.Infrastructure.ExternalServices.Weather.Models;

internal sealed record OpenMeteoDailyForecast(
    DateOnly Date,
    int WeatherCode,
    double TempMax,
    double TempMin);
