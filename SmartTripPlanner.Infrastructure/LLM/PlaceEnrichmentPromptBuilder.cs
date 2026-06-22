using System.Text;
using SmartTripPlanner.Domain.AggregatesModel;

namespace SmartTripPlanner.Infrastructure.LLM;

internal static class PlaceEnrichmentPromptBuilder
{
    public static string Build(Place place, string? tipsText = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Place: {place.Name}");

        var categories = place.Attributes
            .Where(a => a.Key == "category")
            .Select(a => a.Value);
        if (categories.Any())
        {
            sb.AppendLine($"Categories: {string.Join(", ", categories)}");
        }

        if (place.OpeningHours.Count > 0)
        {
            sb.AppendLine("Opening Hours:");
            foreach (var oh in place.OpeningHours.OrderBy(o => o.DayOfWeek))
            {
                var open = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(oh.OpenMinutes));
                var close = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(oh.CloseMinutes));
                sb.AppendLine($"  {oh.DayOfWeek}: {open:HH:mm}-{close:HH:mm}");
            }
        }

        if (!string.IsNullOrWhiteSpace(tipsText))
        {
            sb.AppendLine($"Visitor Tips: {tipsText}");
        }

        sb.AppendLine();
        sb.AppendLine("Respond with valid JSON only in this exact schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"TypicalDurationMinutes\": <int, 15-480>,");
        sb.AppendLine("  \"IsIndoor\": <bool>,");
        sb.AppendLine("  \"FamilyFriendlyScore\": <int, 1-5>,");
        sb.AppendLine("  \"Popularity\": <double, 0.0-1.0>");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
