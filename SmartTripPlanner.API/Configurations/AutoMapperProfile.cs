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

        // Itinerary response mappings
        CreateMap<ActivityNode, ActivityResponse>()
            .ForMember(dest => dest.PlaceId, opt => opt.MapFrom(src => src.PlaceId))
            .ForMember(dest => dest.PlaceName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.DurationMinutes, opt => opt.MapFrom(src => src.DurationMinutes))
            .ForMember(dest => dest.SequenceOrder, opt => opt.MapFrom(src => src.SequenceOrder))
            .ForMember(dest => dest.IsIndoor, opt => opt.MapFrom(src => src.IsIndoor))
            .ForMember(dest => dest.Priority, opt => opt.MapFrom(src => src.Priority.ToString()))
            .ForMember(dest => dest.TransportMode, opt => opt.MapFrom(src => src.TransitToNext != null ? src.TransitToNext.TransportMode.ToString() : string.Empty))
            .ForMember(dest => dest.TransitDurationMinutes, opt => opt.MapFrom(src => src.TransitToNext != null ? src.TransitToNext.DurationMinutes : 0))
            .ForMember(dest => dest.BufferMinutes, opt => opt.MapFrom(src => src.TransitToNext != null ? src.TransitToNext.BufferMinutes : 0))
            .ForMember(dest => dest.FrictionAlert, opt => opt.MapFrom(src => src.TransitToNext != null ? src.TransitToNext.FrictionAlert : false));

        CreateMap<BlockTimeline, BlockResponse>()
            .ForMember(dest => dest.BlockType, opt => opt.MapFrom(src => src.BlockType.ToString()))
            .ForMember(dest => dest.TotalDurationMinutes, opt => opt.MapFrom(src => src.BlockTotalDurationMinutes))
            .ForMember(dest => dest.Activities, opt => opt.MapFrom(src => src.Activities));

        CreateMap<DayPlan, DayPlanResponse>()
            .ForMember(dest => dest.WeatherSummary, opt => opt.MapFrom(src => src.WeatherSummary.ToString()))
            .ForMember(dest => dest.Blocks, opt => opt.MapFrom(src => new[]
            {
                src.Morning,
                src.Afternoon,
                src.Evening
            }));
    }
}
