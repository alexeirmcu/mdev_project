using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class RefreshWeatherHandler(
    ITripRepository tripRepository,
    IWeatherProvider weatherProvider,
    IMapper mapper,
    ILogger<RefreshWeatherHandler> logger,
    IUserContext userContext)
    : IRequestHandler<RefreshWeather, WeatherRefreshResult>
{
    public async Task<WeatherRefreshResult> Handle(RefreshWeather request, CancellationToken ct)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, ct);
        if (trip is null)
            throw new TripNotFoundException(request.TripId);

        if (trip.OwnerUserId != userContext.UserId)
            throw new TripForbiddenException(request.TripId, userContext.UserId);

        if (trip.Days.Count == 0)
        {
            logger.LogInformation("Trip {TripId} has no days — refresh weather is a no-op", request.TripId);
            return new WeatherRefreshResult(false, 0, Array.Empty<DayWeatherChange>());
        }

        var forecast = await weatherProvider.GetWeatherAsync(
            trip.CityId, trip.StartDate, trip.EndDate, ct);

        var changes = new List<DayWeatherChange>();
        bool anyChanged = false;

        foreach (var day in trip.Days)
        {
            if (!forecast.TryGetValue(day.Date, out var newWeather))
                continue;

            if (day.WeatherSummary != newWeather)
            {
                changes.Add(new DayWeatherChange(
                    day.DayIndex,
                    day.WeatherSummary.ToString(),
                    newWeather.ToString()));

                day.SetWeather(newWeather);
                day.MarkStale();
                day.UpdateWeatherTimestamp();
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            await tripRepository.UpdateAsync(trip, ct);
            logger.LogInformation("Trip {TripId} weather refreshed — {Count} day(s) changed",
                request.TripId, changes.Count);
        }
        else
        {
            logger.LogInformation("Trip {TripId} weather refreshed — no changes", request.TripId);
        }

        return new WeatherRefreshResult(anyChanged, changes.Count, changes.AsReadOnly());
    }
}
