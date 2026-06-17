namespace SmartTripPlanner.Domain.ApiModels;

public record TripPreferencesInput(
    bool CarAvailable = false,
    int MaxWalkingMinutes = 30,
    bool WeatherAwareEnabled = true);
