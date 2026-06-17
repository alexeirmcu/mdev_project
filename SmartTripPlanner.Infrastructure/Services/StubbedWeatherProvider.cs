using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Infrastructure.Services;

/// <summary>
/// MVP stubbed weather provider — returns Clear for all dates in range.
/// Real weather API integration is deferred to post-MVP.
/// </summary>
public class StubbedWeatherProvider : IWeatherProvider
{
    public Task<Dictionary<DateOnly, WeatherCondition>> GetWeatherAsync(
        long cityId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        var result = new Dictionary<DateOnly, WeatherCondition>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            result[date] = WeatherCondition.Clear;
        }

        return Task.FromResult(result);
    }
}
