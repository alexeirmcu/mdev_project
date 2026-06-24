namespace SmartTripPlanner.Domain.ApiModels;

public record DayWeatherChange(
    int DayIndex,
    string PreviousWeather,
    string NewWeather);
