using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.ApplicationServices.Behaviors;

public class ValidationRequestException : SmartTripDomainException
{
    public IReadOnlyList<ValidationResult> Errors { get; }

    public ValidationRequestException(IReadOnlyList<ValidationResult> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
