using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Trip : Entity, IAggregateRoot
{
    public required string CityId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public required Location BaseHotel { get; init; }
    public List<DayPlan> Days { get; private set; } = new();
    public ICollection<Place> SelectedPlaces { get; private set; } = new List<Place>();
    public TimeOnly DefaultStartTime { get; private set; } = new TimeOnly(9, 0);

    public void SelectPlace(Place place)
    {
        SelectedPlaces.Add(place);
    }

    public bool UnselectPlace(long placeId)
    {
        var place = SelectedPlaces.FirstOrDefault(p => p.Id == placeId);
        if (place is not null)
        {
            SelectedPlaces.Remove(place);
            return true;
        }
        return false;
    }

    public void GenerateDays()
    {
        Days.Clear();

        var start = StartDate;
        var end = EndDate;
        int dayIndex = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var day = new DayPlan
            {
                DayIndex = dayIndex++,
                Date = date,
                WeatherSummary = WeatherCondition.Clear,
                Morning = new BlockTimeline { BlockType = BlockType.Morning },
                Afternoon = new BlockTimeline { BlockType = BlockType.Afternoon },
                Evening = new BlockTimeline { BlockType = BlockType.Evening }
            };
            day.UpdateStartTime(DefaultStartTime);
            Days.Add(day);
        }
    }
}
