using FluentValidation;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Repository;
using Microsoft.Extensions.Options;

namespace SmartTripPlanner.ApplicationServices.Validators;

public class SearchPlacesRequestValidator : AbstractValidator<SearchPlacesRequest>
{
    public SearchPlacesRequestValidator(ICityRepository cityRepo, IOptions<PlaceSearchOptions> options)
    {
        var opts = options.Value;

        RuleFor(x => x.SearchRequest.Query)
            .NotEmpty().WithErrorCode(nameof(ErrorCode.REQUIRED_FIELD))
                .WithMessage("The search query is required.")
            .MinimumLength(3).WithErrorCode(nameof(ErrorCode.MIN_LENGTH_VIOLATION))
                .WithMessage("The search query must be at least 3 characters long.");

        RuleFor(x => x.SearchRequest.CityCode)
            .NotEmpty().WithErrorCode(nameof(ErrorCode.REQUIRED_FIELD))
                .WithMessage("CityCode is required.")
            .MustAsync(async (cityCode, cancellationToken) =>
            {
                var city = await cityRepo.GetByCodeAsync(cityCode);
                return city is not null && city.IsAllowed;
            })
            .WithErrorCode(nameof(ErrorCode.INVALID_CITY))
            .WithMessage((request, cityCode) =>
                $"City '{cityCode}' is not supported.");

        RuleFor(x => x.SearchRequest.MaxResults)
            .GreaterThanOrEqualTo(1)
                .When(x => x.SearchRequest.MaxResults.HasValue)
            .Must((request, maxResults) => !maxResults.HasValue || maxResults.Value <= opts.MaxResults)
                .WithErrorCode(nameof(ErrorCode.MAX_RESULTS_EXCEEDED))
                .WithMessage($"Max results must be between 1 and {opts.MaxResults}.");
    }
}
