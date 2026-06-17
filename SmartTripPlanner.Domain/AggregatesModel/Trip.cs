using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Trip : Entity, IAggregateRoot
{
    private List<MustSee> _originalMustSees = new();
    private List<DayPlan> _days = new();

    public Guid TripId { get; init; }
    public string TripCode { get; init; } = null!;
    public long CityId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public Location BaseHotel { get; init; } = null!;
    public Travelers Travelers { get; init; } = new Travelers(2, 0, 0);
    public TripPreferences Preferences { get; init; } = new TripPreferences();
    public TimeOnly DefaultStartTime { get; init; } = new TimeOnly(9, 0);
    public IReadOnlyList<MustSee> OriginalMustSees => _originalMustSees.AsReadOnly();
    public IReadOnlyList<DayPlan> Days => _days.AsReadOnly();
    public TripStatus Status { get; private set; } = TripStatus.CREATED;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public void AddMustSee(MustSee mustSee)
    {
        if (_originalMustSees.Any(m => m.PlaceId == mustSee.PlaceId))
            throw new SmartTripDomainException($"PlaceId {mustSee.PlaceId} is already in MustSees");

        _originalMustSees.Add(mustSee);
    }

    public bool RemoveMustSee(long placeId)
    {
        var mustSee = _originalMustSees.FirstOrDefault(m => m.PlaceId == placeId);
        if (mustSee is not null)
        {
            _originalMustSees.Remove(mustSee);
            return true;
        }
        return false;
    }

    public void UpdateStatus(TripStatus newStatus)
    {
        Status = newStatus;
    }

    public void GenerateDays()
    {
        _days.Clear();

        var start = StartDate;
        var end = EndDate;
        int dayIndex = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var day = new DayPlan
            {
                DayIndex = dayIndex++,
                Date = date,
                Morning = new BlockTimeline { BlockType = BlockType.Morning },
                Afternoon = new BlockTimeline { BlockType = BlockType.Afternoon },
                Evening = new BlockTimeline { BlockType = BlockType.Evening }
            };
            day.SetWeather(WeatherCondition.Clear);
            day.UpdateStartTime(DefaultStartTime);
            _days.Add(day);
        }
    }

    public void GenerateDays(IEnumerable<DayPlan> days)
    {
        if (_days.Any())
            throw new SmartTripDomainException("Days have already been generated for this trip");

        _days.AddRange(days);
        Status = TripStatus.GENERATED;
    }
}
