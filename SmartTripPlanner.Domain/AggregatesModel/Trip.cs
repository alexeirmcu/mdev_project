using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Trip : Entity, IAggregateRoot
{
    private const int MaxTripDurationDays = 14;
    private List<MustSee> _originalMustSees = new();
    private List<DayPlan> _days = new();

    public Guid TripId { get; init; }
    public string TripCode { get; init; } = null!;
    public long CityId { get; init; }
    public City City { get; init; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Location? BaseHotel { get; set; }
    public Travelers Travelers { get; set; } = new Travelers(2, 0, 0);
    public TripPreferences Preferences { get; set; } = new TripPreferences();
    public TimeOnly DefaultStartTime { get; set; } = new TimeOnly(9, 0);
    public IReadOnlyList<MustSee> OriginalMustSees => _originalMustSees.AsReadOnly();
    public IReadOnlyList<DayPlan> Days => _days.AsReadOnly();
    public required string OwnerUserId { get; init; }
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

    public void UpdateDates(DateOnly start, DateOnly end)
    {
        if (start > end)
            throw new BusinessRuleException("StartDate cannot be after EndDate.");

        var duration = end.DayNumber - start.DayNumber + 1;
        if (duration > MaxTripDurationDays)
            throw new BusinessRuleException(
                $"Trip duration ({duration} days) exceeds maximum allowed ({MaxTripDurationDays} days)");

        StartDate = start;
        EndDate = end;
    }

    public void UpdateBaseHotel(Location hotel)
    {
        BaseHotel = hotel ?? throw new ArgumentNullException(nameof(hotel));
    }

    public void UpdateTravelers(Travelers travelers)
    {
        Travelers = travelers ?? throw new ArgumentNullException(nameof(travelers));
    }

    public void UpdatePreferences(TripPreferences preferences)
    {
        Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public void UpdateDefaultStartTime(TimeOnly time)
    {
        DefaultStartTime = time;
    }

    public void ClearDaysAndReset()
    {
        _days.Clear();
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
    }
}
