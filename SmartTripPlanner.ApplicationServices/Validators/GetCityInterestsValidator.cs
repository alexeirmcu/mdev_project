using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class GetCityInterestsValidator : AbstractValidator<GetCityInterests>
{
    public GetCityInterestsValidator()
    {
        RuleFor(x => x.CityCode)
            .NotEmpty().WithErrorCode(nameof(ErrorCode.REQUIRED_FIELD))
                .WithMessage("CityCode is required.");
    }
}
