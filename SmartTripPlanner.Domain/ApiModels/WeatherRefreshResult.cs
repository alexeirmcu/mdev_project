namespace SmartTripPlanner.Domain.ApiModels;

public record WeatherRefreshResult(
    bool Updated,
    int DaysRefreshed);
