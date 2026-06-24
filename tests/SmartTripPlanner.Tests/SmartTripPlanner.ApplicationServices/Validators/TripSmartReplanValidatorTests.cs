using FluentValidation.TestHelper;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Validators;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.ApplicationServices.Validators;

[TestClass]
public sealed class TripSmartReplanValidatorTests
{
    private readonly TripSmartReplanValidator _sut = new();

    private static TripSmartReplanRequest CreateValidPayload() => new(
        DateTime.UtcNow,
        "CurrentDay",
        WeatherCondition.Good);

    [TestMethod]
    public async Task ValidRequest_PassesValidation()
    {
        var command = new TripSmartReplan(Guid.NewGuid(), CreateValidPayload(), "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task TripId_WhenEmpty_Fails()
    {
        var command = new TripSmartReplan(Guid.Empty, CreateValidPayload(), "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.TripId);
    }

    [TestMethod]
    public async Task CurrentDateTime_WhenDefault_Fails()
    {
        var payload = CreateValidPayload() with { CurrentDateTime = default };
        var command = new TripSmartReplan(Guid.NewGuid(), payload, "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.CurrentDateTime);
    }

    [TestMethod]
    public async Task Scope_WhenNull_Fails()
    {
        var payload = CreateValidPayload() with { Scope = null! };
        var command = new TripSmartReplan(Guid.NewGuid(), payload, "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.Scope);
    }

    [TestMethod]
    public async Task Scope_WhenEmpty_Fails()
    {
        var payload = CreateValidPayload() with { Scope = "" };
        var command = new TripSmartReplan(Guid.NewGuid(), payload, "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Request.Scope);
    }
}
