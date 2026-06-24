using FluentValidation.TestHelper;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Validators;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.Tests.ApplicationServices.Validators;

[TestClass]
public sealed class ToggleActivityCompletionValidatorTests
{
    private readonly ToggleActivityCompletionValidator _sut = new();

    private static ActivityCompletionRequest CreateValidRequest() => new(42L, true);

    [TestMethod]
    public async Task ValidRequest_PassesValidation()
    {
        var command = new ToggleActivityCompletion(Guid.NewGuid(), 0, 42L, CreateValidRequest(), "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task TripId_WhenEmpty_Fails()
    {
        var command = new ToggleActivityCompletion(Guid.Empty, 0, 42L, CreateValidRequest(), "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.TripId);
    }

    [TestMethod]
    public async Task DayIndex_Negative_Fails()
    {
        var command = new ToggleActivityCompletion(Guid.NewGuid(), -1, 42L, CreateValidRequest(), "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.DayIndex);
    }

    [TestMethod]
    public async Task PlaceId_Zero_Fails()
    {
        var command = new ToggleActivityCompletion(Guid.NewGuid(), 0, 0L, CreateValidRequest(), "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.PlaceId);
    }
}
