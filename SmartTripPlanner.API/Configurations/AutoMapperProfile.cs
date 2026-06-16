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
        CreateMap<PlaceAttribute, PlaceAttributeModel>();

        CreateMap<Place, PlaceModel>()
            .ForMember(dest => dest.Attributes, opt => opt.MapFrom(src => src.Attributes))
            .ForMember(dest => dest.IsAutoUpdateEnabled, opt => opt.MapFrom(src => src.IsAutoUpdateEnabled));
    }
}
