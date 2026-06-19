using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTripPlanner.API.Controllers;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.SmartTripPlanner.API.Controllers;

[TestClass]
public sealed class TripsControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly TripsController _controller;

    public TripsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new TripsController(_mediatorMock.Object);
    }

    private static TripPlanResponse CreateResponseWithDays()
    {
        return new TripPlanResponse(
            Guid.NewGuid(),
            "MAD-2026-TEST",
            1L,
            "madrid-es",
            "Madrid",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            new List<MustSeeResponse>
            {
                new(1L, "High", 0, "Morning")
            },
            "GENERATED",
            "09:00")
        {
            Days = new List<DayPlanResponse>
            {
                new()
                {
                    DayIndex = 0,
                    Date = new DateOnly(2026, 7, 1),
                    WeatherSummary = "Clear",
                    Blocks = new List<BlockResponse>
                    {
                        new()
                        {
                            BlockType = "Morning",
                            TotalDurationMinutes = 135,
                            Activities = new List<ActivityResponse>
                            {
                                new()
                                {
                                    PlaceName = "Museo del Prado",
                                    DurationMinutes = 120,
                                    TransportMode = "WALK_AND_PUBLIC_TRANSPORT",
                                    TransitDurationMinutes = 15
                                }
                            }
                        },
                        new()
                        {
                            BlockType = "Afternoon",
                            TotalDurationMinutes = 60,
                            Activities = new List<ActivityResponse>
                            {
                                new()
                                {
                                    PlaceName = "Palacio Real",
                                    DurationMinutes = 60,
                                    TransportMode = "WALK_AND_PUBLIC_TRANSPORT",
                                    TransitDurationMinutes = 10
                                }
                            }
                        },
                        new()
                        {
                            BlockType = "Evening",
                            TotalDurationMinutes = 0,
                            Activities = new List<ActivityResponse>()
                        }
                    }
                },
                new()
                {
                    DayIndex = 1,
                    Date = new DateOnly(2026, 7, 2),
                    WeatherSummary = "Clear",
                    Blocks = new List<BlockResponse>
                    {
                        new() { BlockType = "Morning", TotalDurationMinutes = 0, Activities = new List<ActivityResponse>() },
                        new() { BlockType = "Afternoon", TotalDurationMinutes = 0, Activities = new List<ActivityResponse>() },
                        new() { BlockType = "Evening", TotalDurationMinutes = 0, Activities = new List<ActivityResponse>() }
                    }
                },
                new()
                {
                    DayIndex = 2,
                    Date = new DateOnly(2026, 7, 3),
                    WeatherSummary = "Clear",
                    Blocks = new List<BlockResponse>
                    {
                        new() { BlockType = "Morning", TotalDurationMinutes = 0, Activities = new List<ActivityResponse>() },
                        new() { BlockType = "Afternoon", TotalDurationMinutes = 0, Activities = new List<ActivityResponse>() },
                        new() { BlockType = "Evening", TotalDurationMinutes = 0, Activities = new List<ActivityResponse>() }
                    }
                }
            }
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 1: POST /api/trips → 201 Created with Days[] in response
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateTrip_Returns201WithDaysInResponse()
    {
        var response = CreateResponseWithDays();
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput> { new(1L, Priority.High) },
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateTrip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var act = await _controller.CreateTrip(request, CancellationToken.None);

        var createdResult = act as CreatedAtActionResult;
        Assert.IsNotNull(createdResult);
        Assert.AreEqual(StatusCodes.Status201Created, createdResult.StatusCode);

        var body = createdResult.Value as TripPlanResponse;
        Assert.IsNotNull(body);
        Assert.IsNotNull(body.Days);
        Assert.AreEqual(3, body.Days.Count);
        Assert.AreEqual("GENERATED", body.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 2: POST /api/trips → response has blocks with activities and transit
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateTrip_ResponseHasBlocksWithActivitiesAndTransit()
    {
        var response = CreateResponseWithDays();
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput> { new(1L, Priority.High) },
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateTrip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var act = await _controller.CreateTrip(request, CancellationToken.None);

        var createdResult = act as CreatedAtActionResult;
        Assert.IsNotNull(createdResult);
        var body = createdResult.Value as TripPlanResponse;
        Assert.IsNotNull(body);

        // Day 0 should have 3 blocks
        var day0 = body.Days[0];
        Assert.AreEqual(3, day0.Blocks.Count);
        Assert.AreEqual("Clear", day0.WeatherSummary);

        // Morning block should have an activity with transit details
        var morning = day0.Blocks[0];
        Assert.AreEqual("Morning", morning.BlockType);
        Assert.IsTrue(morning.TotalDurationMinutes > 0);
        Assert.AreEqual(1, morning.Activities.Count);

        var activity = morning.Activities[0];
        Assert.AreEqual("Museo del Prado", activity.PlaceName);
        Assert.AreEqual(120, activity.DurationMinutes);
        Assert.AreEqual("WALK_AND_PUBLIC_TRANSPORT", activity.TransportMode);
        Assert.AreEqual(15, activity.TransitDurationMinutes);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 3: POST /api/trips → all days have correct block types
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateTrip_AllDaysHaveThreeBlockTypes()
    {
        var response = CreateResponseWithDays();
        var request = new TripGenerationRequest(
            "madrid-es",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new List<MustSeeInput> { new(1L, Priority.High) },
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            "09:00");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateTrip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var act = await _controller.CreateTrip(request, CancellationToken.None);

        var createdResult = act as CreatedAtActionResult;
        Assert.IsNotNull(createdResult);
        var body = createdResult.Value as TripPlanResponse;
        Assert.IsNotNull(body);

        foreach (var day in body.Days)
        {
            var blockTypes = day.Blocks.Select(b => b.BlockType).ToHashSet();
            Assert.IsTrue(blockTypes.Contains("Morning"), $"Day {day.DayIndex} missing Morning");
            Assert.IsTrue(blockTypes.Contains("Afternoon"), $"Day {day.DayIndex} missing Afternoon");
            Assert.IsTrue(blockTypes.Contains("Evening"), $"Day {day.DayIndex} missing Evening");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test 4: GET /api/trips/{id}
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTrip_ReturnsOkWithTrip()
    {
        var tripId = Guid.NewGuid();
        var expectedResponse = new TripPlanResponse(
            tripId,
            "MAD-2026-TEST",
            1L,
            "madrid-es",
            "Madrid",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            new LocationModel("Hotel Central", 40.4168, -3.7038),
            new TravelersInput(2, 0, 0),
            new TripPreferencesInput(false, 30, true),
            new List<MustSeeResponse>(),
            "CREATED",
            "09:00");

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetTrip>(q => q.TripId == tripId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var act = await _controller.GetTrip(tripId, CancellationToken.None);

        var okResult = act as OkObjectResult;
        Assert.IsNotNull(okResult);
        Assert.AreEqual(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.AreSame(expectedResponse, okResult.Value);
    }
}
