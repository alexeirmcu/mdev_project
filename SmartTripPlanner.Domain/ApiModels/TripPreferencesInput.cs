using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.ApiModels;

public record TripPreferencesInput(
    bool CarAvailable = false,
    int MaxWalkingMinutes = 30,
    bool WeatherAwareEnabled = true,
    IReadOnlyList<string>? Interests = null,
    ReturnToHotelStrategy ReturnToHotelStrategy = ReturnToHotelStrategy.Always);
