using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class ToggleActivityCompletionValidator : AbstractValidator<ToggleActivityCompletion>
{
    public ToggleActivityCompletionValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty();

        RuleFor(x => x.DayIndex)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.PlaceId)
            .GreaterThan(0);
    }
}
