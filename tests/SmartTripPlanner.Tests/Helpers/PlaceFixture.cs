using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Tests.Helpers;

public static class PlaceFixture
{
    public static Place CreatePopulatedPlace()
    {
        var location = new PlaceLocation(40.4168, -3.7038);
        var place = new Place("fsq-prado-123", "Museo del Prado", "madrid-es", location,
            120, true, false);

        place.OpeningHours.Add(new OpeningHoursWindow(DayOfWeek.Monday, 600, 1200));
        place.OpeningHours.Add(new OpeningHoursWindow(DayOfWeek.Tuesday, 600, 1200));

        return place;
    }
}
