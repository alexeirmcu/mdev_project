using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class RegenerateDayValidator : AbstractValidator<RegenerateDay>
{
    public RegenerateDayValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty();

        RuleFor(x => x.DayIndex)
            .GreaterThanOrEqualTo(0);
    }
}
