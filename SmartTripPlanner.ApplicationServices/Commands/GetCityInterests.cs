using MediatR;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record GetCityInterests(string CityCode) : IRequest<IReadOnlyList<string>>;
