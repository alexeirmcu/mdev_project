using System.Runtime.Serialization;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.Exceptions;

public class SmartTripDomainException : DomainException
{
    public SmartTripDomainException() { }

    public SmartTripDomainException(string? message) : base(message) { }

    public SmartTripDomainException(string? message, Exception? innerException)
        : base(message, innerException) { }

    protected SmartTripDomainException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
