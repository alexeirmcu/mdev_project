using FluentValidation.TestHelper;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Validators;

namespace SmartTripPlanner.Tests.ApplicationServices.Validators;

[TestClass]
public sealed class RegenerateDayValidatorTests
{
    private readonly RegenerateDayValidator _sut = new();

    [TestMethod]
    public async Task ValidRequest_PassesValidation()
    {
        var command = new RegenerateDay(Guid.NewGuid(), 0, "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task TripId_WhenEmpty_Fails()
    {
        var command = new RegenerateDay(Guid.Empty, 0, "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.TripId);
    }

    [TestMethod]
    public async Task DayIndex_Negative_Fails()
    {
        var command = new RegenerateDay(Guid.NewGuid(), -1, "user-42");
        var result = await _sut.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.DayIndex);
    }
}
