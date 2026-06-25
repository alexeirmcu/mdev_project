using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class ListTripsValidator : AbstractValidator<ListTrips>
{
    public ListTripsValidator()
    {
        RuleFor(x => x.StartDate)
            .Must((query, startDate) =>
                !startDate.HasValue || !query.EndDate.HasValue || startDate.Value <= query.EndDate.Value)
            .WithMessage("StartDate must be less than or equal to EndDate when both are provided.")
            .WithErrorCode(nameof(ErrorCode.VALIDATION_ERROR));
    }
}
