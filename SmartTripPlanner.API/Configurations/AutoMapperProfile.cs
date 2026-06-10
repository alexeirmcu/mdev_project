using AutoMapper;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.API.Configurations;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<PlaceLocation, PlaceLocationModel>();
        CreateMap<OpeningHoursWindow, OpeningHoursWindowModel>();
        CreateMap<Place, PlaceModel>();
    }
}
