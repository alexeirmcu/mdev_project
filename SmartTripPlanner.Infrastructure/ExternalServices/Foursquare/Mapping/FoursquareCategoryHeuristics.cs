using SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Models;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Mapping;

internal static class FoursquareCategoryHeuristics
{
    public static (int TypicalDurationMinutes, bool IsIndoor, bool IsFamilyFriendly)
        Map(IEnumerable<FoursquareCategory> categories)
    {
        var first = categories.FirstOrDefault();
        if (first is null)
            return (60, true, true);

        return first.FsqCategoryId switch
        {
            "10000" or "10035" or "10014" => (120, true, true),
            "10024" or "10025" or "10033" or "10040" => (60, true, true),
            "13003" or "13002" or "13004" => (90, true, true),
            "10008" or "10009" or "10010" => (60, true, false),
            _ => (60, true, true),
        };
    }
}
