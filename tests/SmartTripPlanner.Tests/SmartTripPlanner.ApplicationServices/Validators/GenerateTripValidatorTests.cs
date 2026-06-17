using FluentValidation.TestHelper;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Validators;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.ApplicationServices.Validators;

[TestClass]
public sealed class GenerateTripValidatorTests
{
    private readonly GenerateTripValidator _sut = new();

    private static GenerateTrip CreateValidRequest() => new(new TripGenerationRequest(
        "madrid-es",
        new DateOnly(2026, 7, 1),
        new DateOnly(2026, 7, 3),
        new LocationModel("Hotel Central", 40.4168, -3.7038),
        new List<MustSeeInput> { new(1L, Priority.HIGH) },
        new TravelersInput(2, 0, 0),
        new TripPreferencesInput(false, 30, true),
        "09:00"));

    [TestMethod]
    public async Task ValidRequest_PassesValidation()
    {
        var request = CreateValidRequest();
        var result = await _sut.TestValidateAsync(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task CityCode_WhenEmpty_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "", DateOnly.MaxValue, DateOnly.MaxValue,
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.HIGH) })
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.CityCode);
    }

    [TestMethod]
    public async Task StartDate_InPast_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 3),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.HIGH) })
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.StartDate);
    }

    [TestMethod]
    public async Task EndDate_BeforeStartDate_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 1),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.HIGH) })
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.EndDate);
    }

    [TestMethod]
    public async Task MustSees_Empty_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput>())
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.MustSees);
    }

    [TestMethod]
    public async Task MustSees_DuplicatePlaceIds_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.HIGH), new(1L, Priority.LOW) })
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.MustSees);
    }

    [TestMethod]
    public async Task DefaultStartHour_InvalidFormat_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.HIGH) },
                null, null, "25:00")
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.DefaultStartHour);
    }

    [TestMethod]
    public async Task BaseHotel_Null_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3),
                null!, new List<MustSeeInput> { new(1L, Priority.HIGH) })
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.BaseHotel);
    }
}
