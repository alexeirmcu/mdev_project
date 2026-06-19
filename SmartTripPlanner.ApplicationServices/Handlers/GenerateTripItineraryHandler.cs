using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class GenerateTripItineraryHandler(
    ITripRepository tripRepository,
    IPlaceRepository placeRepository,
    IItineraryGenerator itineraryGenerator,
    IWeatherProvider weatherProvider,
    IMapper mapper,
    ILogger<GenerateTripItineraryHandler> logger)
    : IRequestHandler<GenerateTripItinerary, TripPlanResponse>
{
    public async Task<TripPlanResponse> Handle(GenerateTripItinerary request, CancellationToken ct)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, ct);
        if (trip is null)
            throw new TripNotFoundException(request.TripId);

        if (trip.BaseHotel is null)
            throw new BusinessRuleException("BaseHotel is required to generate an itinerary.");

        var candidates = await placeRepository.GetManyByCityIdAsync(
            trip.CityId,
            trip.Preferences.Interests,
            ct);
        var weather = await weatherProvider.GetWeatherAsync(trip.CityId, trip.StartDate, trip.EndDate, ct);
        await itineraryGenerator.GenerateAsync(trip, candidates, weather, ct);

        await tripRepository.UpdateAsync(trip, ct);

        var response = mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = trip.City);

        logger.LogInformation("Itinerary generated for trip {TripId}", trip.TripId);

        return response;
    }
}
