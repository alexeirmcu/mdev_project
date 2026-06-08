using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Trip : Entity, IAggregateRoot
{
    public required string CityId { get; init; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public required Location BaseHotel { get; init; }
    public List<DayPlan> Days { get; private set; } = new();
    public IReadOnlyList<MustSeeInput> OriginalMustSees { get; private set; } = Array.Empty<MustSeeInput>();
}
