namespace SmartTripPlanner.Domain.Exceptions;

public class BusinessRuleException : SmartTripDomainException
{
    public IReadOnlyList<object>? Details { get; }

    public BusinessRuleException(string? message) : base(message) { }

    public BusinessRuleException(string? message, IReadOnlyList<object>? details)
        : base(message)
    {
        Details = details;
    }
}
