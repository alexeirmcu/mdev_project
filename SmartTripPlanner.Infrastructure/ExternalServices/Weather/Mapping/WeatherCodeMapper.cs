using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Infrastructure.ExternalServices.Weather.Mapping;

internal static class WeatherCodeMapper
{
    public static WeatherCondition Map(int wmoCode)
    {
        // Precipitation codes: drizzle, rain, snow, thunderstorm, hail
        if (IsInRange(wmoCode, 51, 67) ||
            IsInRange(wmoCode, 71, 77) ||
            IsInRange(wmoCode, 80, 82) ||
            IsInRange(wmoCode, 85, 86) ||
            IsInRange(wmoCode, 95, 99))
            return WeatherCondition.Bad;

        // Clear sky
        if (wmoCode == 0)
            return WeatherCondition.Clear;

        // Partly cloudy, overcast without precipitation
        if (IsInRange(wmoCode, 1, 3) || wmoCode == 45 || wmoCode == 48)
            return WeatherCondition.Good;

        // Unmapped codes default to Clear (documented fallback)
        return WeatherCondition.Clear;
    }

    private static bool IsInRange(int value, int low, int high) => value >= low && value <= high;
}
