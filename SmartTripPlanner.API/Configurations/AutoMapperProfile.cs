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
        CreateMap<TripPreferencesInput, TripPreferences>()
            .ForCtorParam("interests", opt => opt.MapFrom(src => src.Interests));
        CreateMap<LocationModel, Location>();

        CreateMap<Travelers, TravelersInput>();
        CreateMap<TripPreferences, TripPreferencesInput>();

        CreateMap<MustSee, MustSeeResponse>()
            .ForMember(dest => dest.Priority, opt => opt.MapFrom(src => src.Priority.ToString()))
            .ForMember(dest => dest.PinnedBlock, opt => opt.MapFrom(src => src.PinnedBlock.HasValue ? src.PinnedBlock.ToString() : null));

        CreateMap<Location, LocationModel>();

        CreateMap<Trip, TripPlanResponse>()
            .ForCtorParam("CityCode", opt => opt.MapFrom((src, ctx) => ((City?)ctx.Items["City"])?.CityCode ?? string.Empty))
            .ForCtorParam("CityName", opt => opt.MapFrom((src, ctx) => ((City?)ctx.Items["City"])?.CityName ?? string.Empty))
            .ForCtorParam("MustSees", opt => opt.MapFrom(src => src.OriginalMustSees))
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Days.Any() ? "GENERATED" : "CREATED"))
            .ForCtorParam("DefaultStartHour", opt => opt.MapFrom(src => src.DefaultStartTime.ToString("HH:mm")))
            .ForMember(dest => dest.Days, opt => opt.MapFrom(src => src.Days));

        // TransitDetails → TransitResponse (used for both inter-activity and hotel transit)
        CreateMap<TransitDetails, TransitResponse>()
            .ForMember(dest => dest.TransportMode, opt => opt.MapFrom(src => src.TransportMode.ToString()));

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
            .ForMember(dest => dest.FrictionAlert, opt => opt.MapFrom(src => src.TransitToNext != null ? src.TransitToNext.FrictionAlert : false))
            .ForMember(dest => dest.EstimatedArrival, opt => opt.MapFrom(src => src.EstimatedArrival))
            .ForMember(dest => dest.EstimatedDeparture, opt => opt.MapFrom(src => src.EstimatedDeparture));

        CreateMap<BlockTimeline, BlockResponse>()
            .ForMember(dest => dest.BlockType, opt => opt.MapFrom(src => src.BlockType.ToString()))
            .ForMember(dest => dest.TotalDurationMinutes, opt => opt.MapFrom(src => src.BlockTotalDurationMinutes))
            .ForMember(dest => dest.Activities, opt => opt.MapFrom(src => src.Activities))
            .ForMember(dest => dest.TransitFromHotel, opt => opt.MapFrom(src => src.TransitFromHotel))
            .ForMember(dest => dest.TransitToHotel, opt => opt.MapFrom(src => src.TransitToHotel));

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
