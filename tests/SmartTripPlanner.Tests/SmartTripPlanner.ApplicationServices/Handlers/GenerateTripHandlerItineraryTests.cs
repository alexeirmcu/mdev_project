using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.ApplicationServices.Handlers;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;
using SmartTripPlanner.Domain.Services;

namespace SmartTripPlanner.Tests.ApplicationServices.Handlers;

[TestClass]
public sealed class GenerateTripHandlerItineraryTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<ITripCodeGenerator> _codeGenMock = new();
    private readonly Mock<IWeatherProvider> _weatherProviderMock = new();
    private readonly Mock<ITransitCalculator> _transitMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<ILogger<GenerateTripHandler>> _loggerMock = new();
    private readonly HeuristicItineraryGenerator _realGenerator;
    private readonly GenerateTripHandler _handler;

    public GenerateTripHandlerItineraryTests()
    {
        _transitMock
            .Setup(t => t.EstimateAsync(
                It.IsAny<PlaceLocation>(),
                It.IsAny<PlaceLocation>(),
                It.IsAny<TransportMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlaceLocation from, PlaceLocation to, TransportMode mode, CancellationToken _) =>
            {
                var distance = from.DistanceKmTo(to);
                var (speed, buffer) = mode switch
                {
                    TransportMode.WALK_AND_PUBLIC_TRANSPORT => (15.0, 10),
                    TransportMode.CAR => (30.0, 5),
                    _ => (15.0, 10)
                };
                var duration = Math.Max(2, (int)Math.Ceiling((distance / speed) * 60.0));
                return new TransitEstimate(duration, buffer,
                    mode == TransportMode.WALK_AND_PUBLIC_TRANSPORT && distance > 2.0);
            });

        _realGenerator = new HeuristicItineraryGenerator(
            new CandidateScorer(),
            _transitMock.Object);

        _tripRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tripRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mapperMock
            .Setup(m => m.Map<LocationModel>(It.IsAny<Location>()))
            .Returns((Location loc) => new LocationModel(loc.Name, loc.Latitude, loc.Longitude));

        _handler = new GenerateTripHandler(
            _tripRepoMock.Object,
            _cityRepoMock.Object,
            _placeRepoMock.Object,
            _codeGenMock.Object,
            _realGenerator,
            _weatherProviderMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    private static City CreateCity()
    {
        var city = new City("madrid-es", "Madrid", true);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(city, 1L);
        return city;
    }

    private static Place CreatePlace(long id, string name, double lat, double lng,
        int duration = 60, bool isIndoor = false)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng),
            duration, isIndoor, true);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);
        foreach (var day in Enum.GetValues<DayOfWeek>())
            place.OpeningHours.Add(new OpeningHoursWindow(day, 480, 1200));
        return place;
    }

    private static void SetupWeatherMock(Mock<IWeatherProvider> mock, DateOnly start, DateOnly end,
        WeatherCondition condition = WeatherCondition.Clear)
    {
        var dict = new Dictionary<DateOnly, WeatherCondition>();
        for (var d = start; d <= end; d = d.AddDays(1))
            dict[d] = condition;

        mock.Setup(w => w.GetWeatherAsync(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dict);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 1: Generate trip with must-sees → response includes DayPlans with blocks
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_WithMustSees_ReturnsResponseWithDayPlans()
    {
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput>
            {
                new(1L, Priority.High),
                new(2L, Priority.Medium)
            },
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCity());

        var mustSeePlace1 = CreatePlace(1, "Museo del Prado", 40.4168, -3.7038, duration: 120);
        var mustSeePlace2 = CreatePlace(2, "Palacio Real", 40.4180, -3.7140, duration: 90);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { mustSeePlace1, mustSeePlace2 });

        // GetManyByCityIdAsync must return ALL places (must-sees + candidates)
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place>
            {
                mustSeePlace1,
                mustSeePlace2,
                CreatePlace(3, "Parque del Retiro", 40.4150, -3.6830, duration: 60),
                CreatePlace(4, "Plaza Mayor", 40.4154, -3.7074, duration: 45)
            });

        _codeGenMock.Setup(g => g.GenerateAsync("madrid-es", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MAD-2026-TEST");

        SetupWeatherMock(_weatherProviderMock, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3));

        var result = await _handler.Handle(new GenerateTrip(request), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Days);
        Assert.AreEqual(3, result.Days.Count);
        Assert.AreEqual("GENERATED", result.Status);

        // Each day should have 3 blocks
        foreach (var day in result.Days)
        {
            Assert.AreEqual(3, day.Blocks.Count);
        }

        // Must-sees should be distributed across days
        var totalActivities = result.Days.Sum(d => d.Blocks.Sum(b => b.Activities.Count));
        Assert.IsTrue(totalActivities >= 2, $"Expected at least 2 activities, got {totalActivities}");

        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Once);
        _tripRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 2: Generate trip with pinned must-see → correct day assignment
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_WithPinnedMustSee_CorrectDayAssignment()
    {
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput>
            {
                new(1L, Priority.High, PinnedDayIndex: 1) // pinned to day 1
            },
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCity());

        var pinnedPlace = CreatePlace(1, "Pinned Place", 40.4168, -3.7038, duration: 60);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { pinnedPlace });

        // GetManyByCityIdAsync must include the must-see place
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { pinnedPlace });

        _codeGenMock.Setup(g => g.GenerateAsync("madrid-es", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MAD-2026-PIN");

        SetupWeatherMock(_weatherProviderMock, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3));

        var result = await _handler.Handle(new GenerateTrip(request), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Days.Count);

        // Day 1 (index 1) should have the pinned activity
        var day1 = result.Days[1];
        var pinnedActivity = day1.Blocks
            .SelectMany(b => b.Activities)
            .FirstOrDefault(a => a.PlaceName == "Pinned Place");
        Assert.IsNotNull(pinnedActivity, "Pinned must-see should appear in day 1");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 3: Generate trip with Bad weather → indoor activities prioritized
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_BadWeather_IndoorActivitiesPrioritized()
    {
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1), // Single day
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput>(), // No must-sees — all candidates
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true), // WeatherAware = true
            "09:00");

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCity());

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place>());

        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place>
            {
                CreatePlace(1, "Indoor Museum", 40.4168, -3.7038, duration: 60, isIndoor: true),
                CreatePlace(2, "Outdoor Park", 40.4170, -3.7040, duration: 60, isIndoor: false),
                CreatePlace(3, "Outdoor Plaza", 40.4172, -3.7042, duration: 60, isIndoor: false),
            });

        _codeGenMock.Setup(g => g.GenerateAsync("madrid-es", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MAD-2026-WTH");

        // Bad weather on July 1
        SetupWeatherMock(_weatherProviderMock, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1),
            WeatherCondition.Bad);

        var result = await _handler.Handle(new GenerateTrip(request), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Days.Count);

        // First activity in morning should be indoor (highest score in bad weather)
        var morning = result.Days[0].Blocks.First(b => b.BlockType == "Morning");
        if (morning.Activities.Count > 0)
        {
            Assert.AreEqual("Indoor Museum", morning.Activities[0].PlaceName);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 4: Generate trip over-constrained → throws OverConstrainedRouteException
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_OverConstrained_ThrowsOverConstrainedRouteException()
    {
        // Single day with 7 High-priority must-sees → fills all capacity exactly
        // But 8 should overflow and throw
        var mustSeeInputs = Enumerable.Range(1, 8)
            .Select(i => new MustSeeInput(i, Priority.High))
            .ToList();

        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1), // Single day — very constrained
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            mustSeeInputs,
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCity());

        var manyPlaces = Enumerable.Range(1, 8)
            .Select(i => CreatePlace(i, $"Place {i}", 40.4168 + i * 0.0005, -3.7038 + i * 0.0005))
            .ToList();

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyPlaces);

        // GetManyByCityIdAsync must include ALL places (must-see places)
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyPlaces);

        _codeGenMock.Setup(g => g.GenerateAsync("madrid-es", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MAD-2026-OVR");

        SetupWeatherMock(_weatherProviderMock, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1));

        var ex = await CatchExceptionAsync<OverConstrainedRouteException>(() =>
            _handler.Handle(new GenerateTrip(request), CancellationToken.None));

        Assert.IsNotNull(ex);
        Assert.IsTrue(ex.ConflictingPlaceIds.Count > 0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 5: Handler with zero must-sees — still generates with candidates only
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_NoMustSees_GeneratesWithCandidatesOnly()
    {
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput>(), // No must-sees
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCity());

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place>());

        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place>
            {
                CreatePlace(1, "Candidate A", 40.4168, -3.7038, duration: 60),
                CreatePlace(2, "Candidate B", 40.4154, -3.7074, duration: 60),
                CreatePlace(3, "Candidate C", 40.4180, -3.7140, duration: 60),
            });

        _codeGenMock.Setup(g => g.GenerateAsync("madrid-es", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MAD-2026-NON");

        SetupWeatherMock(_weatherProviderMock, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 2));

        var result = await _handler.Handle(new GenerateTrip(request), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Days.Count);
        Assert.AreEqual("GENERATED", result.Status);

        // Should have some activities from candidates
        var totalActivities = result.Days.Sum(d => d.Blocks.Sum(b => b.Activities.Count));
        Assert.IsTrue(totalActivities > 0, "Expected at least some candidates placed");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 6: Response has correct block types and activity details
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_WithMustSees_ResponseHasCorrectBlockStructure()
    {
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput>
            {
                new(1L, Priority.High),
                new(2L, Priority.Medium)
            },
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCity());

        var mustSeeA = CreatePlace(1, "Must See A", 40.4168, -3.7038, duration: 60);
        var mustSeeB = CreatePlace(2, "Must See B", 40.4154, -3.7074, duration: 60);

        _placeRepoMock.Setup(r => r.GetManyByIdsAsync(
                It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { mustSeeA, mustSeeB });

        // GetManyByCityIdAsync must include must-see places
        _placeRepoMock.Setup(r => r.GetManyByCityIdAsync(
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Place> { mustSeeA, mustSeeB });

        _codeGenMock.Setup(g => g.GenerateAsync("madrid-es", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MAD-2026-BLK");

        SetupWeatherMock(_weatherProviderMock, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1));

        var result = await _handler.Handle(new GenerateTrip(request), CancellationToken.None);

        Assert.IsNotNull(result);
        var day = result.Days[0];
        var blockTypes = day.Blocks.Select(b => b.BlockType).ToHashSet();
        Assert.IsTrue(blockTypes.Contains("Morning"));
        Assert.IsTrue(blockTypes.Contains("Afternoon"));
        Assert.IsTrue(blockTypes.Contains("Evening"));
        Assert.IsTrue(day.Blocks.Any(b => b.Activities.Count > 0),
            "At least one block should have activities");
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
