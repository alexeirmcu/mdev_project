using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class TripSmartReplanValidator : AbstractValidator<TripSmartReplan>
{
    public TripSmartReplanValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty();

        RuleFor(x => x.Request.CurrentDateTime)
            .NotEmpty();

        RuleFor(x => x.Request.Scope)
            .NotEmpty();

        RuleFor(x => x.Request.CurrentBlockWeather)
            .IsInEnum();
    }
}
