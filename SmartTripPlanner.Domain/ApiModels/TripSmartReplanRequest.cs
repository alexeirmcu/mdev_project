using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.ApiModels;

public record TripSmartReplanRequest(
    DateTime CurrentDateTime,
    string Scope,
    WeatherCondition CurrentBlockWeather);
