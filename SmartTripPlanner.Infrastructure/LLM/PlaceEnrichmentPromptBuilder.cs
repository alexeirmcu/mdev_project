using System.Text;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.Infrastructure.LLM;

internal sealed class PlaceEnrichmentPromptBuilder(IPromptTemplateProvider templateProvider)
{
    public string Build(Place place, string? tipsText)
    {
        var template = templateProvider.GetTemplate("PlaceEnrichment");
        var replacements = new Dictionary<string, string>
        {
            ["Name"] = EscapeQuotes(place.Name),
            ["CategoriesSection"] = BuildCategoriesSection(place),
            ["OpeningHoursSection"] = BuildOpeningHoursSection(place),
            ["VisitorTipsSection"] = BuildVisitorTipsSection(tipsText),
            ["Schema"] = BuildSchema()
        };

        return ApplyTemplate(template.UserPromptTemplate, replacements);
    }

    private static string ApplyTemplate(string template, Dictionary<string, string> replacements)
    {
        return replacements.Aggregate(template, (current, pair) => current.Replace($"{{{{{pair.Key}}}}}", pair.Value));
    }

    private static string BuildCategoriesSection(Place place)
    {
        var categories = place.Attributes
            .Where(a => a.Key == "category")
            .Select(a => a.Value)
            .ToList();

        if (!categories.Any()) return string.Empty;
        return $"Categories: {string.Join(", ", categories)}\n";
    }

    private static string BuildOpeningHoursSection(Place place)
    {
        if (place.OpeningHours.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Opening Hours:");
        foreach (var oh in place.OpeningHours.OrderBy(o => o.DayOfWeek))
        {
            var open = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(oh.OpenMinutes));
            var close = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(oh.CloseMinutes));
            sb.AppendLine($"  {oh.DayOfWeek}: {open:HH:mm}-{close:HH:mm}");
        }
        return sb.ToString();
    }

    private static string BuildVisitorTipsSection(string? tipsText)
    {
        if (string.IsNullOrWhiteSpace(tipsText)) return string.Empty;
        return $"Visitor Tips: {EscapeQuotes(tipsText)}\n";
    }

    private static string BuildSchema()
    {
        return "{\n  \"TypicalDurationMinutes\": <int, 15-480>,\n  \"IsIndoor\": <bool>,\n  \"FamilyFriendlyScore\": <int, 1-5>,\n  \"Popularity\": <double, 0.0-1.0>\n}";
    }

    private static string EscapeQuotes(string input)
    {
        return input.Replace("\"", "\\\"");
    }
}
