using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class TransitDetails : ValueObject
{
    public TransportMode TransportMode { get; private set; }
    public int DurationMinutes { get; private set; }
    public int BufferMinutes { get; private set; }
    public bool FrictionAlert { get; private set; }

    public TransitDetails(TransportMode transportMode, int durationMinutes, int bufferMinutes, bool frictionAlert)
    {
        TransportMode = transportMode;
        DurationMinutes = durationMinutes;
        BufferMinutes = bufferMinutes;
        FrictionAlert = frictionAlert;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TransportMode;
        yield return DurationMinutes;
        yield return BufferMinutes;
        yield return FrictionAlert;
    }
}
