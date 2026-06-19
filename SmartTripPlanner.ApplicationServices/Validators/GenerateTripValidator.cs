using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class GenerateTripValidator : AbstractValidator<GenerateTrip>
{
    public GenerateTripValidator()
    {
        RuleFor(x => x.Payload.CityCode)
            .NotEmpty().WithErrorCode(nameof(ErrorCode.REQUIRED_FIELD))
                .WithMessage("CityCode is required.")
            .MaximumLength(50);

        RuleFor(x => x.Payload.StartDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("StartDate cannot be in the past.");

        RuleFor(x => x.Payload.EndDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.Payload.StartDate)
            .WithMessage("EndDate must be greater than or equal to StartDate.");

        RuleFor(x => x.Payload.DefaultStartHour)
            .NotEmpty()
            .Matches("^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$")
            .WithMessage("Invalid time format. Expected HH:mm.");

        RuleFor(x => x.Payload.BaseHotel)
            .ChildRules(hotel =>
            {
                hotel.RuleFor(h => h!.Name).NotEmpty().MaximumLength(200);
                hotel.RuleFor(h => h!.Latitude).InclusiveBetween(-90, 90);
                hotel.RuleFor(h => h!.Longitude).InclusiveBetween(-180, 180);
            })
            .When(x => x.Payload.BaseHotel is not null);

        RuleFor(x => x.Payload.MustSees)
            .Must(list => list!.Select(m => m.PlaceId).Distinct().Count() == list!.Count)
            .WithMessage("Duplicate PlaceIds are not allowed in MustSees.")
            .When(x => x.Payload.MustSees is not null && x.Payload.MustSees.Count > 0);

        RuleFor(x => x.Payload.Travelers)
            .NotNull().WithErrorCode(nameof(ErrorCode.REQUIRED_FIELD))
                .WithMessage("Travelers is required.")
            .ChildRules(t =>
            {
                t.RuleFor(x => x!.Adults).GreaterThanOrEqualTo(1);
                t.RuleFor(x => x!.Children).GreaterThanOrEqualTo(0);
                t.RuleFor(x => x!.Infants).GreaterThanOrEqualTo(0);
                t.RuleFor(x => x!.Adults + x.Children + x.Infants).LessThanOrEqualTo(10);
            });

        RuleFor(x => x.Payload.Preferences)
            .ChildRules(p =>
            {
                p.RuleFor(x => x!.MaxWalkingMinutes).InclusiveBetween(5, 120);
            })
            .When(x => x.Payload.Preferences is not null);

        RuleFor(x => x.Payload.Preferences!.Interests)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one interest is required.")
            .When(x => x.Payload.Preferences is not null);
    }
}
