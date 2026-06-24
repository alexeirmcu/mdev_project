using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Services;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class CandidateScorerTests
{
    private readonly CandidateScorer _scorer = new();

    private static Place CreatePlace(bool isFamilyFriendly = true, bool isIndoor = false)
    {
        var place = new Place("fsq_test", "Test Place", 1, new PlaceLocation(40.4168, -3.7038),
            60, isIndoor, isFamilyFriendly);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, 1L);
        return place;
    }

    [TestMethod]
    public void Score_FamilyTripWithFamilyPlace_GetsBonus()
    {
        var place = CreatePlace(isFamilyFriendly: true);
        var context = new ScoringContext(
            IsFamilyTrip: true,
            IsBadWeather: false,
            DistanceFromBlockCenterKm: 0);

        var score = _scorer.Score(place, context);
        // Family bonus (15) + popularity (0.5 * 20 = 10) - distance (0) = 25
        Assert.AreEqual(25, score, 0.001);
    }

    [TestMethod]
    public void Score_NonFamilyTrip_NoFamilyBonus()
    {
        var place = CreatePlace(isFamilyFriendly: true);
        var context = new ScoringContext(
            IsFamilyTrip: false,
            IsBadWeather: false,
            DistanceFromBlockCenterKm: 0);

        var score = _scorer.Score(place, context);
        // popularity (0.5 * 20 = 10) - distance (0) = 10
        Assert.AreEqual(10, score, 0.001);
    }

    [TestMethod]
    public void Score_DistancePenalty_ReducesScore()
    {
        var place = CreatePlace();
        var context = new ScoringContext(
            IsFamilyTrip: false,
            IsBadWeather: false,
            DistanceFromBlockCenterKm: 2.0);

        var score = _scorer.Score(place, context);
        // popularity (10) - distance (2 * 5 = 10) = 0
        Assert.AreEqual(0, score, 0.001);
    }

    [TestMethod]
    public void Score_IndoorOnBadWeather_GetsBonus()
    {
        var place = CreatePlace(isIndoor: true);
        var context = new ScoringContext(
            IsFamilyTrip: false,
            IsBadWeather: true,
            DistanceFromBlockCenterKm: 0);

        var score = _scorer.Score(place, context);
        // popularity (10) + weather bonus (20) = 30
        Assert.AreEqual(30, score, 0.001);
    }

    [TestMethod]
    public void Score_OutdoorOnBadWeather_GetsPenalty()
    {
        var place = CreatePlace(isIndoor: false);
        var context = new ScoringContext(
            IsFamilyTrip: false,
            IsBadWeather: true,
            DistanceFromBlockCenterKm: 0);

        var score = _scorer.Score(place, context);
        // popularity (10) + weather penalty (-20) = -10
        Assert.AreEqual(-10, score, 0.001);
    }

    [TestMethod]
    public void Score_AllFactorsCombined_CorrectTotal()
    {
        var place = CreatePlace(isFamilyFriendly: true, isIndoor: true);
        var context = new ScoringContext(
            IsFamilyTrip: true,
            IsBadWeather: true,
            DistanceFromBlockCenterKm: 1.0);

        var score = _scorer.Score(place, context);
        // family (15) + popularity (10) - distance (5) + weather indoor (20) = 40
        Assert.AreEqual(40, score, 0.001);
    }

    [TestMethod]
    public void Score_ForcedOutdoorOnBadWeather_SkipsPenaltyAndBonus()
    {
        var place = CreatePlace(isIndoor: false);
        var context = new ScoringContext(
            IsFamilyTrip: false,
            IsBadWeather: true,
            DistanceFromBlockCenterKm: 0,
            ForceIncludeDespiteWeather: true);

        var score = _scorer.Score(place, context);
        // popularity (10) only — no penalty, no bonus since forced outdoor
        Assert.AreEqual(10, score, 0.001);
    }

    [TestMethod]
    public void Score_NonForcedOutdoorOnBadWeather_StillPenalized()
    {
        var place = CreatePlace(isIndoor: false);
        var context = new ScoringContext(
            IsFamilyTrip: false,
            IsBadWeather: true,
            DistanceFromBlockCenterKm: 0,
            ForceIncludeDespiteWeather: false);

        var score = _scorer.Score(place, context);
        // popularity (10) + penalty (-20) = -10
        Assert.AreEqual(-10, score, 0.001);
    }

    [TestMethod]
    public void Score_ForcedIndoorOnBadWeather_StillGetsBonus()
    {
        var place = CreatePlace(isIndoor: true);
        var context = new ScoringContext(
            IsFamilyTrip: false,
            IsBadWeather: true,
            DistanceFromBlockCenterKm: 0,
            ForceIncludeDespiteWeather: true);

        var score = _scorer.Score(place, context);
        // popularity (10) + indoor bonus (20) = 30 (force only skips for outdoor)
        Assert.AreEqual(30, score, 0.001);
    }

    [TestMethod]
    public void Score_HighPopularity_OutranksLowPopularity()
    {
        var placeA = CreatePlace();
        var placeB = CreatePlace();

        var contextHigh = new ScoringContext(false, false, 0, PopularityRaw: 1.0);
        var contextLow = new ScoringContext(false, false, 0, PopularityRaw: 0.1);

        var scoreHigh = _scorer.Score(placeA, contextHigh);
        var scoreLow = _scorer.Score(placeB, contextLow);

        Assert.IsTrue(scoreHigh > scoreLow);
    }
}
