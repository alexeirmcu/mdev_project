using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class UpdateTripValidator : AbstractValidator<UpdateTrip>
{
    public UpdateTripValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty().WithMessage("TripId is required.");

        RuleFor(x => x.Payload.MustSeesToAdd)
            .Must(list => list is null || list.Select(m => m.PlaceId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate PlaceIds are not allowed in MustSeesToAdd.")
            .When(x => x.Payload.MustSeesToAdd is not null);
    }
}
