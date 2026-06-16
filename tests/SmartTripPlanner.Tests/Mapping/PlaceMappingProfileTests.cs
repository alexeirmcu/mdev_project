using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.API.Configurations;
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
    public void Map_PlaceToPlaceModel_ConfigurationIsValid()
    {
        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
    }
}
