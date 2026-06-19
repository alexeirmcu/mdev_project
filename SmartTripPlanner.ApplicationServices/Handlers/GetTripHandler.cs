using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class GetTripHandler(
    ITripRepository tripRepository,
    IMapper mapper,
    ILogger<GetTripHandler> logger)
    : IRequestHandler<GetTrip, TripPlanResponse>
{
    public async Task<TripPlanResponse> Handle(GetTrip request, CancellationToken ct)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, ct);
        if (trip is null)
            throw new TripNotFoundException(request.TripId);

        var response = mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = trip.City);

        logger.LogInformation("Trip {TripId} retrieved", trip.TripId);

        return response;
    }
}
