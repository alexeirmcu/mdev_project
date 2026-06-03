using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.ApiModels;

public record TripReplanRequest(
    DateTime CurrentDateTime,
    LocationModel CurrentLocation,
    WeatherCondition CurrentBlockWeather);
