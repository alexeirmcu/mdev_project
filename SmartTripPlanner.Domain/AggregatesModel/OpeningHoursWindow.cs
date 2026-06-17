using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class OpeningHoursWindow : ValueObject
{
    public DayOfWeek DayOfWeek { get; }
    public int OpenMinutes { get; }
    public int CloseMinutes { get; }

    private OpeningHoursWindow() { }

    public OpeningHoursWindow(DayOfWeek dayOfWeek, int openMinutes, int closeMinutes)
    {
        if (openMinutes < 0 || openMinutes > 1439)
            throw new SmartTripDomainException("Open minutes must be between 0 and 1439.");
        if (closeMinutes < 0 || closeMinutes > 1439)
            throw new SmartTripDomainException("Close minutes must be between 0 and 1439.");
        if (openMinutes > closeMinutes)
            throw new SmartTripDomainException("Open minutes must be less than or equal to close minutes.");

        DayOfWeek = dayOfWeek;
        OpenMinutes = openMinutes;
        CloseMinutes = closeMinutes;
    }

    public bool IsOpenOn(DayOfWeek day) => DayOfWeek == day;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return DayOfWeek;
        yield return OpenMinutes;
        yield return CloseMinutes;
    }
}
