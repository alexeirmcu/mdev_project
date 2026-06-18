using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class GenerateTripItineraryValidator : AbstractValidator<GenerateTripItinerary>
{
    public GenerateTripItineraryValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty();
    }
}
