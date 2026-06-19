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
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class GetTripHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly IMapper _mapper;
    private readonly GetTripHandler _handler;

    public GetTripHandlerTests()
    {
        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        var config = new MapperConfiguration(expression, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _handler = new GetTripHandler(
            _tripRepoMock.Object,
            _mapper,
            Mock.Of<ILogger<GetTripHandler>>());
    }

    [TestMethod]
    public async Task Handle_TripFound_ReturnsTripPlanResponse()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        var city = new City("madrid-es", "Madrid", true);
        SetEntityId(city, 1L);

        var trip = new Trip
        {
            TripId = tripId,
            TripCode = "MAD-2026-TEST",
            CityId = 1L,
            City = city,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 3),
            BaseHotel = new Location("Hotel Central", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        // Act
        var result = await _handler.Handle(new GetTrip(tripId), CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(tripId, result.TripId);
        Assert.AreEqual("MAD-2026-TEST", result.TripCode);
        Assert.AreEqual(1L, result.CityId);
        Assert.AreEqual("madrid-es", result.CityCode);
        Assert.AreEqual("Madrid", result.CityName);
        Assert.AreEqual(new DateOnly(2026, 7, 1), result.StartDate);
        Assert.AreEqual(new DateOnly(2026, 7, 3), result.EndDate);
        Assert.AreEqual("CREATED", result.Status);

        _tripRepoMock.Verify(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_TripNotFound_ThrowsTripNotFoundException()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        _tripRepoMock.Setup(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        // Act
        var exception = await CatchExceptionAsync<TripNotFoundException>(
            () => _handler.Handle(new GetTrip(tripId), CancellationToken.None));

        // Assert
        Assert.IsNotNull(exception);
        _tripRepoMock.Verify(r => r.GetByIdAsync(tripId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void SetEntityId<T>(T entity, long id) where T : class
    {
        var entityType = typeof(global::SmartTripPlanner.Domain.Base.Entity);
        var field = entityType.GetField("_Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.SetValue(entity, id);
    }

    private static async Task<T?> CatchExceptionAsync<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
            return null;
        }
        catch (T ex)
        {
            return ex;
        }
    }
}
