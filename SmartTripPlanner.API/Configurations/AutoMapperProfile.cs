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

        // Trip mappings
        CreateMap<MustSeeInput, MustSee>();
        CreateMap<TravelersInput, Travelers>();
        CreateMap<TripPreferencesInput, TripPreferences>();
        CreateMap<LocationModel, Location>();

        CreateMap<MustSee, MustSeeResponse>()
            .ForMember(dest => dest.Priority, opt => opt.MapFrom(src => src.Priority.ToString()))
            .ForMember(dest => dest.PinnedBlock, opt => opt.MapFrom(src => src.PinnedBlock.HasValue ? src.PinnedBlock.ToString() : null));

        CreateMap<Location, LocationModel>();
    }
}
