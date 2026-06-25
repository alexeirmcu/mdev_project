using FluentValidation.TestHelper;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Validators;

namespace SmartTripPlanner.Tests.ApplicationServices.Validators;

[TestClass]
public sealed class ListTripsValidatorTests
{
    private readonly ListTripsValidator _sut = new();

    // ─────────────────────────────────────────────────────────────────────────────
    // Valid cases
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task NoFilters_Passes()
    {
        var query = new ListTrips(null, null, null);
        var result = await _sut.TestValidateAsync(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task StartDateOnly_Passes()
    {
        var query = new ListTrips(null, new DateOnly(2026, 6, 1), null);
        var result = await _sut.TestValidateAsync(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task EndDateOnly_Passes()
    {
        var query = new ListTrips(null, null, new DateOnly(2026, 6, 30));
        var result = await _sut.TestValidateAsync(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task StartDateEqualsEndDate_Passes()
    {
        var query = new ListTrips(null, new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 15));
        var result = await _sut.TestValidateAsync(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task StartDateBeforeEndDate_Passes()
    {
        var query = new ListTrips(null, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        var result = await _sut.TestValidateAsync(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Invalid cases
    // ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task StartDateAfterEndDate_Fails()
    {
        var query = new ListTrips(null, new DateOnly(2026, 7, 1), new DateOnly(2026, 6, 30));
        var result = await _sut.TestValidateAsync(query);
        result.ShouldHaveValidationErrorFor(x => x.StartDate);
    }
}
