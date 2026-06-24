using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record RefreshWeather(Guid TripId, string UserId) : IRequest<WeatherRefreshResult>;
