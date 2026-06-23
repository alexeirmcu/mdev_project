using Microsoft.Extensions.Logging;
using Moq;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather;
using SmartTripPlanner.Infrastructure.ExternalServices.Weather.Models;

namespace SmartTripPlanner.Tests.Infrastructure.ExternalServices.Weather;

[TestClass]
public sealed class OpenMeteoWeatherProviderTests
{
    private static readonly City MadridWithCoords = new("madrid", "Madrid", true, 40.4168, -3.7038);
    private static readonly City MadridWithoutCoords = new("madrid", "Madrid");

    private static OpenMeteoDailyForecast CreateForecast(DateOnly date, int code, double tMax, double tMin)
        => new(date, code, tMax, tMin);

    [TestMethod]
    public async Task GetWeatherAsync_CoordinatesPresent_ReturnsMappedConditions()
    {
        var cityRepo = new Mock<ICityRepository>();
        cityRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MadridWithCoords);

        var forecasts = new List<OpenMeteoDailyForecast>
        {
            CreateForecast(new DateOnly(2026, 6, 25), 0, 28.0, 15.0),  // Clear
            CreateForecast(new DateOnly(2026, 6, 26), 95, 22.0, 12.0), // Bad (thunderstorm)
            CreateForecast(new DateOnly(2026, 6, 27), 1, 20.0, 10.0)   // Good
        };
        var apiClient = new Mock<IWeatherApiClient>();
        apiClient.Setup(c => c.GetForecastAsync(40.4168, -3.7038,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecasts);

        var logger = new Mock<ILogger<OpenMeteoWeatherProvider>>();
        var provider = new OpenMeteoWeatherProvider(cityRepo.Object, apiClient.Object, logger.Object);

        var result = await provider.GetWeatherAsync(1,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 27));

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(WeatherCondition.Clear, result[new DateOnly(2026, 6, 25)]);
        Assert.AreEqual(WeatherCondition.Bad, result[new DateOnly(2026, 6, 26)]);
        Assert.AreEqual(WeatherCondition.Good, result[new DateOnly(2026, 6, 27)]);
    }

    [TestMethod]
    public async Task GetWeatherAsync_MissingCoordinates_ReturnsAllClear()
    {
        var cityRepo = new Mock<ICityRepository>();
        cityRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MadridWithoutCoords);

        var apiClient = new Mock<IWeatherApiClient>();
        var logger = new Mock<ILogger<OpenMeteoWeatherProvider>>();
        var provider = new OpenMeteoWeatherProvider(cityRepo.Object, apiClient.Object, logger.Object);

        var result = await provider.GetWeatherAsync(1,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 27));

        Assert.AreEqual(3, result.Count);
        foreach (var condition in result.Values)
            Assert.AreEqual(WeatherCondition.Clear, condition);

        logger.Verify(
            x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);

        apiClient.Verify(c => c.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetWeatherAsync_ApiReturnsEmpty_ReturnsAllClearAndLogsWarning()
    {
        var cityRepo = new Mock<ICityRepository>();
        cityRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MadridWithCoords);

        var apiClient = new Mock<IWeatherApiClient>();
        apiClient.Setup(c => c.GetForecastAsync(40.4168, -3.7038,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OpenMeteoDailyForecast>());

        var logger = new Mock<ILogger<OpenMeteoWeatherProvider>>();
        var provider = new OpenMeteoWeatherProvider(cityRepo.Object, apiClient.Object, logger.Object);

        var result = await provider.GetWeatherAsync(1,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 27));

        Assert.AreEqual(3, result.Count);
        foreach (var condition in result.Values)
            Assert.AreEqual(WeatherCondition.Clear, condition);

        logger.Verify(
            x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GetWeatherAsync_OutOfHorizonDates_ReturnClear()
    {
        var cityRepo = new Mock<ICityRepository>();
        cityRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MadridWithCoords);

        var forecasts = new List<OpenMeteoDailyForecast>
        {
            CreateForecast(new DateOnly(2026, 6, 25), 0, 28.0, 15.0)
        };
        var apiClient = new Mock<IWeatherApiClient>();
        apiClient.Setup(c => c.GetForecastAsync(40.4168, -3.7038,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecasts);

        var logger = new Mock<ILogger<OpenMeteoWeatherProvider>>();
        var provider = new OpenMeteoWeatherProvider(cityRepo.Object, apiClient.Object, logger.Object);

        // 3-day range but only 1 forecast returned
        var result = await provider.GetWeatherAsync(1,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 27));

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(WeatherCondition.Clear, result[new DateOnly(2026, 6, 25)]); // from forecast
        Assert.AreEqual(WeatherCondition.Clear, result[new DateOnly(2026, 6, 26)]); // out of horizon = Clear
        Assert.AreEqual(WeatherCondition.Clear, result[new DateOnly(2026, 6, 27)]); // out of horizon = Clear
    }

    [TestMethod]
    public async Task GetWeatherAsync_CityNotFound_ReturnsAllClear()
    {
        var cityRepo = new Mock<ICityRepository>();
        cityRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((City?)null);

        var apiClient = new Mock<IWeatherApiClient>();
        var logger = new Mock<ILogger<OpenMeteoWeatherProvider>>();
        var provider = new OpenMeteoWeatherProvider(cityRepo.Object, apiClient.Object, logger.Object);

        var result = await provider.GetWeatherAsync(1,
            new DateOnly(2026, 6, 25), new DateOnly(2026, 6, 27));

        Assert.AreEqual(3, result.Count);
        foreach (var condition in result.Values)
            Assert.AreEqual(WeatherCondition.Clear, condition);

        logger.Verify(
            x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);

        apiClient.Verify(c => c.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
