using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class ListTripsHandler(
    ITripRepository tripRepository,
    ICityRepository cityRepository,
    IMapper mapper,
    ILogger<ListTripsHandler> logger,
    IUserContext userContext)
    : IRequestHandler<ListTrips, List<TripSummaryResponse>>
{
    public async Task<List<TripSummaryResponse>> Handle(ListTrips request, CancellationToken ct)
    {
        long? cityId = null;

        if (request.CityCode is not null)
        {
            var city = await cityRepository.GetByCodeAsync(request.CityCode, ct);
            if (city is null)
            {
                logger.LogWarning("CityCode {CityCode} not found — returning empty list", request.CityCode);
                return new List<TripSummaryResponse>();
            }

            cityId = city.Id;
        }

        var trips = await tripRepository.ListAsync(
            userContext.UserId,
            cityId,
            request.StartDate,
            request.EndDate,
            ct);

        var result = mapper.Map<List<TripSummaryResponse>>(trips);

        logger.LogInformation("Listed {Count} trips for user {UserId}", result.Count, userContext.UserId);

        return result;
    }
}
