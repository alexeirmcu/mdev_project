using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record SearchPlacesResponse(IReadOnlyList<PlaceModel> Results);
