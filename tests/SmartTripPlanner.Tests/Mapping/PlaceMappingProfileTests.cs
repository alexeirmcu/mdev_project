using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.API.Configurations;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Tests.Helpers;

namespace SmartTripPlanner.Tests.Mapping;

[TestClass]
public sealed class PlaceMappingProfileTests
{
    private static IMapper CreateMapper()
    {
        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    [TestMethod]
    public void Map_PlaceToPlaceModel_MapsAllFieldsCorrectly()
    {
        var mapper = CreateMapper();
        var place = PlaceFixture.CreatePopulatedPlace();

        var model = mapper.Map<PlaceModel>(place);

        Assert.AreEqual(place.ProviderReferenceId, model.ProviderReferenceId);
        Assert.AreEqual(place.Name, model.Name);
        Assert.AreEqual(place.CityId, model.CityId);
        Assert.AreEqual(place.TypicalDurationMinutes, model.TypicalDurationMinutes);
        Assert.AreEqual(place.IsIndoor, model.IsIndoor);
        Assert.AreEqual(place.IsFamilyFriendly, model.IsFamilyFriendly);
        Assert.AreEqual(place.IsAutoUpdateEnabled, model.IsAutoUpdateEnabled);
    }

    [TestMethod]
    public void Map_PlaceToPlaceModel_FlattensLocation()
    {
        var mapper = CreateMapper();
        var place = PlaceFixture.CreatePopulatedPlace();

        var model = mapper.Map<PlaceModel>(place);

        Assert.IsNotNull(model.Location);
        Assert.AreEqual(place.Location.Latitude, model.Location.Latitude);
        Assert.AreEqual(place.Location.Longitude, model.Location.Longitude);
    }

    [TestMethod]
    public void Map_PlaceToPlaceModel_MapsOpeningHours()
    {
        var mapper = CreateMapper();
        var place = PlaceFixture.CreatePopulatedPlace();

        var model = mapper.Map<PlaceModel>(place);

        Assert.IsNotNull(model.OpeningHours);
        Assert.AreEqual(2, model.OpeningHours.Count);

        var first = model.OpeningHours[0];
        Assert.AreEqual(place.OpeningHours[0].DayOfWeek, first.DayOfWeek);
        Assert.AreEqual(place.OpeningHours[0].OpenMinutes, first.OpenMinutes);
        Assert.AreEqual(place.OpeningHours[0].CloseMinutes, first.CloseMinutes);

        var second = model.OpeningHours[1];
        Assert.AreEqual(place.OpeningHours[1].DayOfWeek, second.DayOfWeek);
        Assert.AreEqual(place.OpeningHours[1].OpenMinutes, second.OpenMinutes);
        Assert.AreEqual(place.OpeningHours[1].CloseMinutes, second.CloseMinutes);
    }

    [TestMethod]
    public void Map_PlaceToPlaceModel_MapsAttributes()
    {
        var mapper = CreateMapper();
        var place = PlaceFixture.CreatePopulatedPlace();

        var model = mapper.Map<PlaceModel>(place);

        Assert.IsNotNull(model.Attributes);
        Assert.AreEqual(2, model.Attributes.Count);

        var first = model.Attributes[0];
        Assert.AreEqual("category", first.Key);
        Assert.AreEqual("Museum", first.Value);

        var second = model.Attributes[1];
        Assert.AreEqual("chain", second.Key);
        Assert.AreEqual("Prado", second.Value);
    }

    [TestMethod]
    public void Map_ActivityNodeToActivityResponse_OvertimeAlertTrue()
    {
        var mapper = CreateMapper();
        var activity = new ActivityNode(1, "Test", 1, 60);
        activity.MarkOvertime();

        var response = mapper.Map<ActivityResponse>(activity);

        Assert.IsTrue(response.OvertimeAlert);
    }

    [TestMethod]
    public void Map_ActivityNodeToActivityResponse_OvertimeAlertFalse()
    {
        var mapper = CreateMapper();
        var activity = new ActivityNode(1, "Test", 1, 60);

        var response = mapper.Map<ActivityResponse>(activity);

        Assert.IsFalse(response.OvertimeAlert);
    }

    [TestMethod]
    public void Map_PlaceToPlaceModel_ConfigurationIsValid()
    {
        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
    }

    [TestMethod]
    public void Map_MustSeeToMustSeeResponse_MapsForceIncludeDespiteWeather()
    {
        var mapper = CreateMapper();
        var mustSee = new MustSee(42L, global::SmartTripPlanner.Domain.Enums.Priority.High, forceIncludeDespiteWeather: true);

        var response = mapper.Map<MustSeeResponse>(mustSee);

        Assert.IsTrue(response.ForceIncludeDespiteWeather);
    }

    [TestMethod]
    public void Map_MustSeeToMustSeeResponse_ForceIncludeDespiteWeatherDefaultsFalse()
    {
        var mapper = CreateMapper();
        var mustSee = new MustSee(42L, global::SmartTripPlanner.Domain.Enums.Priority.High);

        var response = mapper.Map<MustSeeResponse>(mustSee);

        Assert.IsFalse(response.ForceIncludeDespiteWeather);
    }

    [TestMethod]
    public void Map_DayPlanToDayPlanResponse_MapsIsStale()
    {
        var mapper = CreateMapper();
        var day = new DayPlan(
            0,
            new DateOnly(2026, 7, 1),
            new BlockTimeline { BlockType = global::SmartTripPlanner.Domain.Enums.BlockType.Morning },
            new BlockTimeline { BlockType = global::SmartTripPlanner.Domain.Enums.BlockType.Afternoon },
            new BlockTimeline { BlockType = global::SmartTripPlanner.Domain.Enums.BlockType.Evening }
        );
        day.MarkStale();

        var response = mapper.Map<DayPlanResponse>(day);

        Assert.IsTrue(response.IsStale);
    }

    [TestMethod]
    public void Map_DayPlanToDayPlanResponse_IsStaleDefaultsFalse()
    {
        var mapper = CreateMapper();
        var day = new DayPlan(
            0,
            new DateOnly(2026, 7, 1),
            new BlockTimeline { BlockType = global::SmartTripPlanner.Domain.Enums.BlockType.Morning },
            new BlockTimeline { BlockType = global::SmartTripPlanner.Domain.Enums.BlockType.Afternoon },
            new BlockTimeline { BlockType = global::SmartTripPlanner.Domain.Enums.BlockType.Evening }
        );

        var response = mapper.Map<DayPlanResponse>(day);

        Assert.IsFalse(response.IsStale);
    }
}
