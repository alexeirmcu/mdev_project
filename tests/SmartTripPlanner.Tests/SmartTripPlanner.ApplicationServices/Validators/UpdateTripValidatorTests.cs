using FluentValidation.TestHelper;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Validators;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.ApplicationServices.Validators;

[TestClass]
public sealed class UpdateTripValidatorTests
{
    private readonly UpdateTripValidator _sut = new();

    [TestMethod]
    public async Task EmptyTripId_Fails()
    {
        var request = new UpdateTrip(Guid.Empty, new TripUpdateRequest());
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.TripId);
    }

    [TestMethod]
    public async Task ValidTripId_Passes()
    {
        var request = new UpdateTrip(Guid.NewGuid(), new TripUpdateRequest());
        var result = await _sut.TestValidateAsync(request);
        result.ShouldNotHaveValidationErrorFor(x => x.TripId);
    }

    [TestMethod]
    public async Task DuplicatePlaceIdsInAdd_Fails()
    {
        var request = new UpdateTrip(Guid.NewGuid(), new TripUpdateRequest(
            MustSeesToAdd: new List<MustSeeInput>
            {
                new(1L, Priority.HIGH),
                new(1L, Priority.LOW)
            }));
        var result = await _sut.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Payload.MustSeesToAdd);
    }

    [TestMethod]
    public async Task UniquePlaceIdsInAdd_Passes()
    {
        var request = new UpdateTrip(Guid.NewGuid(), new TripUpdateRequest(
            MustSeesToAdd: new List<MustSeeInput>
            {
                new(1L, Priority.HIGH),
                new(2L, Priority.LOW)
            }));
        var result = await _sut.TestValidateAsync(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Payload.MustSeesToAdd);
    }
}
