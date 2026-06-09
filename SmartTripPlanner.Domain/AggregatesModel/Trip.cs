using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Trip : Entity, IAggregateRoot
{
    public required string CityId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public required Location BaseHotel { get; init; }
    public List<DayPlan> Days { get; private set; } = new();
    public List<SelectedAttraction> SelectedAttractions { get; private set; } = new();
    public TimeOnly DefaultStartTime { get; private set; } = new TimeOnly(9, 0);

    public void AddSelectedAttraction(string placeId, string name)
    {
        SelectedAttractions.Add(new SelectedAttraction(placeId, name));
    }

    public bool RemoveSelectedAttraction(string placeId)
    {
        var item = SelectedAttractions.FirstOrDefault(sa => sa.PlaceId == placeId);
        if (item is not null)
        {
            SelectedAttractions.Remove(item);
            return true;
        }
        return false;
    }
}
