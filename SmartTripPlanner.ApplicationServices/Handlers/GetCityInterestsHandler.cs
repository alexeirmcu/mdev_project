using MediatR;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class GetCityInterestsHandler(
    ICityRepository cityRepository,
    IPlaceRepository placeRepository)
    : IRequestHandler<GetCityInterests, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetCityInterests request, CancellationToken ct)
    {
        var city = await cityRepository.GetByCodeAsync(request.CityCode, ct);
        if (city is null)
            throw new CityNotFoundException(request.CityCode);

        return await placeRepository.GetDistinctInterestsByCityIdAsync(city.Id, ct);
    }
}
