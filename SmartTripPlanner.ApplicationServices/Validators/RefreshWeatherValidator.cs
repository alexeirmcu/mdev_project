using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class RefreshWeatherValidator : AbstractValidator<RefreshWeather>
{
    public RefreshWeatherValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty();
    }
}
