using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartTripPlanner.API.Configurations;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class ListTripsHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IUserContext> _userContextMock = new();
    private readonly IMapper _mapper;
    private readonly ListTripsHandler _handler;

    public ListTripsHandlerTests()
    {
        _userContextMock.Setup(u => u.UserId).Returns("user-42");

        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _handler = new ListTripsHandler(
            _tripRepoMock.Object,
            _cityRepoMock.Object,
            _mapper,
            Mock.Of<ILogger<ListTripsHandler>>(),
            _userContextMock.Object);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static City CreateCity(string cityCode = "madrid-es", string cityName = "Madrid")
    {
        var city = new City(cityCode, cityName, true);
        SetEntityId(city, 1L);
        return city;
    }

    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static Trip CreateTrip(Guid? tripId = null, long cityId = 1L, City? city = null,
        string ownerUserId = "user-42", DateOnly? startDate = null, DateOnly? endDate = null)
    {
        return new Trip
        {
            TripId = tripId ?? Guid.NewGuid(),
            TripCode = "MAD-2026-TEST",
            CityId = cityId,
            City = city ?? CreateCity(),
            StartDate = startDate ?? FutureStartDate,
            EndDate = endDate ?? FutureStartDate.AddDays(3),
            BaseHotel = new Location("Hotel Central", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            OwnerUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Trip CreateTripWithDaysAndActivities(bool markSomeCompleted = false)
    {
        var city = CreateCity();
        var trip = CreateTrip(city: city);
        trip.GenerateDaysFrom(trip.StartDate);

        // Day 0 - Morning: 1 activity, Afternoon: 1 activity, Evening: 0
        trip.Days[0].GetBlock(BlockType.Morning).ForceAddActivity(
            new ActivityNode(1L, "Museo del Prado", 0, 120, false, null, Priority.High));
        trip.Days[0].GetBlock(BlockType.Afternoon).ForceAddActivity(
            new ActivityNode(2L, "Palacio Real", 1, 90, false, null, Priority.Medium));

        // Day 1 - Morning: 2 activities, Afternoon: 0, Evening: 1
        trip.Days[1].GetBlock(BlockType.Morning).ForceAddActivity(
            new ActivityNode(3L, "Retiro Park", 2, 60, false, null, Priority.Medium));
        trip.Days[1].GetBlock(BlockType.Morning).ForceAddActivity(
            new ActivityNode(4L, "Mercado San Miguel", 3, 45, false, null, Priority.Low));
        trip.Days[1].GetBlock(BlockType.Evening).ForceAddActivity(
            new ActivityNode(5L, "Flamenco Show", 4, 120, false, null, Priority.Medium));

        if (markSomeCompleted)
        {
            trip.Days[0].GetBlock(BlockType.Morning).Activities[0].SetCompleted(true);
            trip.Days[1].GetBlock(BlockType.Morning).Activities[0].SetCompleted(true);
        }

        return trip;
    }

    private static void SetEntityId<T>(T entity, long id) where T : class
    {
        var entityType = typeof(global::SmartTripPlanner.Domain.Base.Entity);
        var field = entityType.GetField("_Id",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.SetValue(entity, id);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_NoFilters_ReturnsAllOwnerTrips()
    {
        // Arrange
        var trips = new List<Trip> { CreateTrip(), CreateTrip() };
        _tripRepoMock
            .Setup(r => r.ListAsync("user-42", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trips);

        // Act
        var result = await _handler.Handle(new ListTrips(null, null, null), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        _tripRepoMock.Verify(
            r => r.ListAsync("user-42", null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithValidCityCode_FiltersByCity()
    {
        // Arrange
        var city = CreateCity();
        var trips = new List<Trip> { CreateTrip(city: city) };
        _cityRepoMock
            .Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);
        _tripRepoMock
            .Setup(r => r.ListAsync("user-42", 1L, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trips);

        // Act
        var result = await _handler.Handle(
            new ListTrips("madrid-es", null, null), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        _cityRepoMock.Verify(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(
            r => r.ListAsync("user-42", 1L, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_CityCodeNotFound_ReturnsEmptyList()
    {
        // Arrange
        _cityRepoMock
            .Setup(r => r.GetByCodeAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((City?)null);

        // Act
        var result = await _handler.Handle(
            new ListTrips("nonexistent", null, null), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
        _cityRepoMock.Verify(r => r.GetByCodeAsync("nonexistent", It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(
            r => r.ListAsync(It.IsAny<string>(), It.IsAny<long?>(),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_EmptyList_ReturnsEmptyResult()
    {
        // Arrange
        _tripRepoMock
            .Setup(r => r.ListAsync("user-42", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trip>());

        // Act
        var result = await _handler.Handle(new ListTrips(null, null, null), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task Handle_WithDateFilters_PassesDatesToListAsync()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var endDate = startDate.AddDays(29);
        _tripRepoMock
            .Setup(r => r.ListAsync("user-42", null, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trip>());

        // Act
        var result = await _handler.Handle(
            new ListTrips(null, startDate, endDate), CancellationToken.None);

        // Assert
        _tripRepoMock.Verify(
            r => r.ListAsync("user-42", null, startDate, endDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_CreatedTrip_ReturnsZeroCounts()
    {
        // Arrange
        var trip = CreateTrip();
        // No GenerateDays call → CREATED status, Days = empty
        _tripRepoMock
            .Setup(r => r.ListAsync("user-42", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trip> { trip });

        // Act
        var result = await _handler.Handle(new ListTrips(null, null, null), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(0, result[0].CompletedActivitiesCount);
        Assert.AreEqual(0, result[0].TotalActivitiesCount);
    }

    [TestMethod]
    public async Task Handle_GeneratedTrip_ComputesActivityCounts()
    {
        // Arrange
        var trip = CreateTripWithDaysAndActivities(markSomeCompleted: true);
        _tripRepoMock
            .Setup(r => r.ListAsync("user-42", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trip> { trip });

        // Act
        var result = await _handler.Handle(new ListTrips(null, null, null), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);

        // 5 activities total (1+1+0 for Day0, 2+0+1 for Day1)
        Assert.AreEqual(5, result[0].TotalActivitiesCount);
        // 2 completed: Museo del Prado, Retiro Park
        Assert.AreEqual(2, result[0].CompletedActivitiesCount);
    }

    [TestMethod]
    public async Task Handle_GeneratedTrip_TotalMustSees()
    {
        // Arrange
        var city = CreateCity();
        var trip = CreateTrip(city: city);
        trip.AddMustSee(new MustSee(1L, "Place 1", Priority.High));
        trip.AddMustSee(new MustSee(2L, "Place 2", Priority.Medium));
        trip.AddMustSee(new MustSee(3L, "Place 3", Priority.Low));

        _tripRepoMock
            .Setup(r => r.ListAsync("user-42", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trip> { trip });

        // Act
        var result = await _handler.Handle(new ListTrips(null, null, null), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(3, result[0].TotalMustSees);
    }

    [TestMethod]
    public async Task Handle_OtherOwnerTrips_NotIncluded()
    {
        // Arrange - repository filters by owner, so returning empty for "user-42"
        _tripRepoMock
            .Setup(r => r.ListAsync("user-42", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trip>());

        // Act
        var result = await _handler.Handle(new ListTrips(null, null, null), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
        _tripRepoMock.Verify(
            r => r.ListAsync("user-42", null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
