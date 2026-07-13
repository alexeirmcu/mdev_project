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

    private static DateOnly FutureStartDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

    private static GenerateTrip CreateValidRequest() => new(new TripGenerationRequest(
        "madrid-es",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
        new LocationModel("Hotel Central", 40.4168, -3.7038),
        new List<MustSeeInput> { new(1L, Priority.High) },
        new TravelersInput(2, 0, 0),
        new TripPreferencesInput(false, 30, true, new List<string> { "culture", "food" }),
        "09:00"), "user-42");

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
                new List<MustSeeInput> { new(1L, Priority.High) })
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
                new List<MustSeeInput> { new(1L, Priority.High) })
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
                "madrid-es", FutureStartDate.AddDays(2), FutureStartDate,
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.High) })
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.EndDate);
    }

    [TestMethod]
    public async Task MustSees_DuplicatePlaceIds_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", FutureStartDate, FutureStartDate.AddDays(2),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.High), new(1L, Priority.Low) },
                new TravelersInput(2, 0, 0))
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
                "madrid-es", FutureStartDate, FutureStartDate.AddDays(2),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.High) },
                new TravelersInput(2, 0, 0), null, "25:00")
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.DefaultStartHour);
    }

    [TestMethod]
    public async Task MustSees_Empty_DoesNotFail()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", FutureStartDate, FutureStartDate.AddDays(2),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput>(),
                new TravelersInput(2, 0, 0))
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Payload.MustSees);
    }

    [TestMethod]
    public async Task MustSees_Null_DoesNotFail()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", FutureStartDate, FutureStartDate.AddDays(2),
                new LocationModel("H", 0, 0),
                MustSees: null,
                Travelers: new TravelersInput(2, 0, 0))
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Payload.MustSees);
    }

    [TestMethod]
    public async Task BaseHotel_Null_DoesNotFail()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", FutureStartDate, FutureStartDate.AddDays(2),
                BaseHotel: null,
                MustSees: new List<MustSeeInput> { new(1L, Priority.High) },
                Travelers: new TravelersInput(2, 0, 0))
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Payload.BaseHotel);
    }

    [TestMethod]
    public async Task Travelers_Null_Fails()
    {
        var request = CreateValidRequest() with
        {
            Payload = new TripGenerationRequest(
                "madrid-es", FutureStartDate, FutureStartDate.AddDays(2),
                new LocationModel("H", 0, 0),
                new List<MustSeeInput> { new(1L, Priority.High) },
                Travelers: null)
        };
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.Travelers);
    }
}
