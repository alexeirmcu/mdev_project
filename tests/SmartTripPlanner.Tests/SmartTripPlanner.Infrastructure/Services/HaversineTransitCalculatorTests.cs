using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Infrastructure.Services;

namespace SmartTripPlanner.Tests.Infrastructure.Services;

[TestClass]
public sealed class HaversineTransitCalculatorTests
{
    private readonly HaversineTransitCalculator _calculator = new();

    [TestMethod]
    public async Task EstimateAsync_WalkingMode_ReturnsReasonableDuration()
    {
        var from = new PlaceLocation(40.4168, -3.7038); // Puerta del Sol
        var to = new PlaceLocation(40.4154, -3.7074);   // Plaza Mayor

        var estimate = await _calculator.EstimateAsync(from, to, TransportMode.WALK_AND_PUBLIC_TRANSPORT);

        // ~0.3 km at 15 km/h PT speed = ~1.2 min, min 2 min
        Assert.IsTrue(estimate.DurationMinutes >= 2);
        Assert.IsTrue(estimate.DurationMinutes <= 5);
        Assert.AreEqual(10, estimate.BufferMinutes);
    }

    [TestMethod]
    public async Task EstimateAsync_CarMode_ReturnsShorterDuration()
    {
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(41.3874, 2.1686); // Madrid to Barcelona ~505 km

        var estimate = await _calculator.EstimateAsync(from, to, TransportMode.CAR);

        // ~505 km at 30 km/h = ~1010 min
        Assert.IsTrue(estimate.DurationMinutes > 500);
        Assert.AreEqual(5, estimate.BufferMinutes);
    }

    [TestMethod]
    public async Task EstimateAsync_CarMode_FasterThanPT()
    {
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(40.4381, -3.6953); // ~2.4 km

        var car = await _calculator.EstimateAsync(from, to, TransportMode.CAR);
        var pt = await _calculator.EstimateAsync(from, to, TransportMode.WALK_AND_PUBLIC_TRANSPORT);

        // Car: ~2.4 km at 30 km/h = ~5 min
        // PT: ~2.4 km at 15 km/h = ~10 min
        Assert.IsTrue(car.DurationMinutes < pt.DurationMinutes);
    }

    [TestMethod]
    public async Task EstimateAsync_SameLocation_MinimumDuration()
    {
        var loc = new PlaceLocation(40.4168, -3.7038);

        var estimate = await _calculator.EstimateAsync(loc, loc, TransportMode.WALK_AND_PUBLIC_TRANSPORT);

        Assert.AreEqual(2, estimate.DurationMinutes); // minimum
        Assert.AreEqual(10, estimate.BufferMinutes);
    }

    [TestMethod]
    public async Task EstimateAsync_LongWalkingDistance_SetsFrictionAlert()
    {
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(40.4500, -3.6900); // ~3.7 km

        var estimate = await _calculator.EstimateAsync(from, to, TransportMode.WALK_AND_PUBLIC_TRANSPORT);

        Assert.IsTrue(estimate.FrictionAlert);
    }

    [TestMethod]
    public async Task EstimateAsync_ShortDistance_NoFrictionAlert()
    {
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(40.4154, -3.7074); // ~0.3 km

        var estimate = await _calculator.EstimateAsync(from, to, TransportMode.WALK_AND_PUBLIC_TRANSPORT);

        Assert.IsFalse(estimate.FrictionAlert);
    }

    [TestMethod]
    public async Task EstimateAsync_CarMode_NeverFrictionAlert()
    {
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(40.4500, -3.6900); // ~3.7 km

        var estimate = await _calculator.EstimateAsync(from, to, TransportMode.CAR);

        Assert.IsFalse(estimate.FrictionAlert);
    }

    [TestMethod]
    public async Task EstimateAsync_KnownDistance_AccurateDuration()
    {
        // Madrid (Puerta del Sol) to Prado Museum: ~0.6 km
        var sol = new PlaceLocation(40.4168, -3.7038);
        var prado = new PlaceLocation(40.4140, -3.6920);

        var estimate = await _calculator.EstimateAsync(sol, prado, TransportMode.WALK_AND_PUBLIC_TRANSPORT);

        // At 15 km/h: 0.6 km / 15 * 60 = ~2.4 min, ceil = 3 min, min 2 min
        Assert.IsTrue(estimate.DurationMinutes >= 2, "Duration should be at least minimum (2 min)");
        Assert.IsTrue(estimate.DurationMinutes <= 5, "Duration should be reasonable for ~0.6 km");
    }

    [TestMethod]
    public async Task EstimateAsync_CarMode_BufferIs5()
    {
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(40.4154, -3.7074);

        var estimate = await _calculator.EstimateAsync(from, to, TransportMode.CAR);

        Assert.AreEqual(5, estimate.BufferMinutes);
    }

    [TestMethod]
    public async Task EstimateAsync_PublicTransportMode_BufferIs10()
    {
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(40.4154, -3.7074);

        var estimate = await _calculator.EstimateAsync(from, to, TransportMode.WALK_AND_PUBLIC_TRANSPORT);

        Assert.AreEqual(10, estimate.BufferMinutes);
    }

    [TestMethod]
    public async Task EstimateAsync_CarMode_TwiceAsFastAsPublicTransport()
    {
        // 10 km distance
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(40.5000, -3.6000); // ~10 km

        var pt = await _calculator.EstimateAsync(from, to, TransportMode.WALK_AND_PUBLIC_TRANSPORT);
        var car = await _calculator.EstimateAsync(from, to, TransportMode.CAR);

        // Car at 30 km/h should be roughly half the time of PT at 15 km/h
        Assert.IsTrue(car.DurationMinutes <= pt.DurationMinutes,
            "Car should be faster than or equal to PT for the same distance");
        Assert.IsTrue(car.DurationMinutes * 2 >= pt.DurationMinutes - 2,
            "Car should be roughly 2x faster than PT (accounting for ceil rounding)");
    }

    [TestMethod]
    public async Task EstimateAsync_Distance_0km_MinimumDuration()
    {
        var loc = new PlaceLocation(40.4168, -3.7038);

        var pt = await _calculator.EstimateAsync(loc, loc, TransportMode.WALK_AND_PUBLIC_TRANSPORT);
        var car = await _calculator.EstimateAsync(loc, loc, TransportMode.CAR);

        Assert.AreEqual(2, pt.DurationMinutes);
        Assert.AreEqual(2, car.DurationMinutes);
    }

    [TestMethod]
    public async Task EstimateAsync_PT_10km_EstimateReasonable()
    {
        // 10 km at 15 km/h = 40 min
        var from = new PlaceLocation(40.4168, -3.7038);
        var to = new PlaceLocation(40.5060, -3.6000);

        var estimate = await _calculator.EstimateAsync(from, to, TransportMode.WALK_AND_PUBLIC_TRANSPORT);

        // Should be around 40 min for 10 km at 15 km/h
        Assert.IsTrue(estimate.DurationMinutes >= 30);
        Assert.IsTrue(estimate.DurationMinutes <= 55);
    }
}
