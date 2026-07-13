using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Services;
using Moq;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class CandidateFillerTests
{
    private readonly Mock<ICandidateScorer> _scorerMock;
    private readonly CandidateFiller _filler;

    public CandidateFillerTests()
    {
        _scorerMock = new Mock<ICandidateScorer>();
        _scorerMock
            .Setup(s => s.Score(It.IsAny<Place>(), It.IsAny<ScoringContext>()))
            .Returns(10.0);

        _filler = new CandidateFiller(_scorerMock.Object);
    }

    private static Place CreatePlace(long id, string name, double lat, double lng, int duration = 60)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng), duration, false, true);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);

        foreach (var day in Enum.GetValues<DayOfWeek>())
            place.OpeningHours.Add(new OpeningHoursWindow(day, 480, 1200));

        return place;
    }

    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static Trip CreateTrip(IReadOnlyList<MustSee> mustSees, int dayCount = 3)
    {
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = "TEST",
            CityId = 1,
            StartDate = FutureStartDate,
            EndDate = FutureStartDate.AddDays(dayCount - 1),
            BaseHotel = new Location("Hotel", 40.4168, -3.7038),
            Travelers = new Travelers(2, 0, 0),
            Preferences = new TripPreferences(),
            DefaultStartTime = new TimeOnly(9, 0),
            OwnerUserId = "user-42",
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var ms in mustSees)
            trip.AddMustSee(ms);

        trip.GenerateDaysFrom(trip.StartDate);

        return trip;
    }

    [TestMethod]
    public async Task FillAsync_EmptyCandidatePool_DoesNothing()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 1);
        await _filler.FillAsync(trip, new List<Place>(), new Dictionary<DateOnly, WeatherCondition>(), CancellationToken.None);

        // No activities added
        Assert.AreEqual(0, trip.Days[0].GetBlock(BlockType.Morning).Activities.Count);
    }

    [TestMethod]
    public async Task FillAsync_PlacesCandidatesInAvailableSlots()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 1);
        var candidates = new List<Place>
        {
            CreatePlace(1, "Candidate A", 40.4168, -3.7038, duration: 60),
            CreatePlace(2, "Candidate B", 40.4170, -3.7040, duration: 60),
            CreatePlace(3, "Candidate C", 40.4180, -3.7050, duration: 60),
        };
        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        };

        await _filler.FillAsync(trip, candidates, weather, CancellationToken.None);

        // Morning should have candidates (3 slots, 3 candidates = 3 placed)
        Assert.AreEqual(3, trip.Days[0].GetBlock(BlockType.Morning).Activities.Count);
        Assert.AreEqual(0, trip.Days[0].GetBlock(BlockType.Afternoon).Activities.Count); // morning fills first
    }

    [TestMethod]
    public async Task FillAsync_UsesHaversineDistance_NotStub()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 1);

        // Place a must-see first to create a reference activity for distance
        var mustSee = new MustSee(1, "MustSee", Priority.High);
        trip.AddMustSee(mustSee);
        var mustSeePlace = CreatePlace(1, "MustSee", 40.4168, -3.7038, duration: 60);
        var candidates = new List<Place>
        {
            CreatePlace(2, "Nearby", 40.4170, -3.7040, duration: 60),  // ~0.03 km from MustSee
            CreatePlace(3, "Far", 41.0000, -2.0000, duration: 60),     // ~170 km away
        };

        // Track what was passed as DistanceFromBlockCenterKm
        double? capturedDistance = null;
        _scorerMock
            .Setup(s => s.Score(It.IsAny<Place>(), It.IsAny<ScoringContext>()))
            .Callback<Place, ScoringContext>((_, ctx) => capturedDistance = ctx.DistanceFromBlockCenterKm)
            .Returns(10.0);

        // Manually place the must-see
        trip.GenerateDaysFrom(trip.StartDate);
        var activity = ItineraryGeneratorHelpers.CreateActivityNode(mustSeePlace, 1);
        trip.Days[0].AddActivity(BlockType.Morning, activity);

        // The must-see is now in candidate pool? No — only non-must-see places are candidates.
        // Rebuild: only candidates that aren't must-sees
        var allPlaces = new List<Place> { mustSeePlace, CreatePlace(2, "Nearby", 40.4170, -3.7040, duration: 60) };
        var candidatePool = allPlaces.Where(p => p.Id != 1).ToList();

        await _filler.FillAsync(trip, candidatePool, new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        // Distance should be > 0 (real haversine, not stub 1.0)
        Assert.IsNotNull(capturedDistance, "Scorer should have been called with a distance");
        Assert.IsTrue(capturedDistance > 0, "Distance should be a real Haversine value, not the stub 1.0");
        Assert.IsTrue(capturedDistance < 0.5, "Candidate nearby should have small distance");
    }

    [TestMethod]
    public async Task FillAsync_ScoringContextCorrectlyPopulated()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 1);
        var candidates = new List<Place>
        {
            CreatePlace(1, "Test Place", 40.4168, -3.7038, duration: 60),
        };

        ScoringContext? capturedContext = null;
        _scorerMock
            .Setup(s => s.Score(It.IsAny<Place>(), It.IsAny<ScoringContext>()))
            .Callback<Place, ScoringContext>((_, ctx) => capturedContext = ctx)
            .Returns(10.0);

        var weather = new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Bad }
        };

        await _filler.FillAsync(trip, candidates, weather, CancellationToken.None);

        Assert.IsNotNull(capturedContext);
        Assert.IsFalse(capturedContext.IsFamilyTrip, "No children in trip");
        Assert.IsTrue(capturedContext.IsBadWeather, "Bad weather should propagate");
        Assert.AreEqual(0, capturedContext.DistanceFromBlockCenterKm, "Empty block => 0 distance");
    }

    [TestMethod]
    public async Task FillAsync_EmptyBlock_ZeroDistance()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 1);
        var candidates = new List<Place>
        {
            CreatePlace(1, "Only Candidate", 40.4168, -3.7038, duration: 60),
        };

        double? capturedDistance = null;
        _scorerMock
            .Setup(s => s.Score(It.IsAny<Place>(), It.IsAny<ScoringContext>()))
            .Callback<Place, ScoringContext>((_, ctx) => capturedDistance = ctx.DistanceFromBlockCenterKm)
            .Returns(10.0);

        await _filler.FillAsync(trip, candidates, new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        // Empty block → distance should be 0
        Assert.AreEqual(0, capturedDistance);
    }

    [TestMethod]
    public async Task FillAsync_PassesPlacePopularityToScoringContext()
    {
        var trip = CreateTrip(Array.Empty<MustSee>(), dayCount: 1);
        var place = CreatePlace(1, "Test Place", 40.4168, -3.7038, duration: 60);
        place.MarkEnriched(60, false, 3, 0.85);
        var candidates = new List<Place> { place };

        double? capturedPopularity = null;
        _scorerMock
            .Setup(s => s.Score(It.IsAny<Place>(), It.IsAny<ScoringContext>()))
            .Callback<Place, ScoringContext>((_, ctx) => capturedPopularity = ctx.PopularityRaw)
            .Returns(10.0);

        await _filler.FillAsync(trip, candidates, new Dictionary<DateOnly, WeatherCondition>
        {
            { trip.StartDate, WeatherCondition.Clear }
        }, CancellationToken.None);

        Assert.IsNotNull(capturedPopularity);
        Assert.AreEqual(0.85, capturedPopularity, "Should use Place.Popularity, not hardcoded 0.5");
    }
}
