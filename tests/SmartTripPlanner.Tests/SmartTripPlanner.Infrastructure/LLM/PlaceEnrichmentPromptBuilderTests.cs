using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Infrastructure.LLM;

namespace SmartTripPlanner.Tests.Infrastructure.LLM;

[TestClass]
public sealed class PlaceEnrichmentPromptBuilderTests
{
    [TestMethod]
    public void Build_WithPlace_ContainsName()
    {
        var place = CreatePlace("Prado Museum");

        var prompt = PlaceEnrichmentPromptBuilder.Build(place);

        Assert.IsTrue(prompt.Contains("Prado Museum"));
    }

    [TestMethod]
    public void Build_WithPlace_ContainsCategories()
    {
        var place = CreatePlace("Prado Museum");
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "museum"));
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "art"));

        var prompt = PlaceEnrichmentPromptBuilder.Build(place);

        Assert.IsTrue(prompt.Contains("museum, art") || prompt.Contains("art, museum"));
    }

    [TestMethod]
    public void Build_WithPlace_ContainsOpeningHours()
    {
        var place = CreatePlace("Prado Museum");
        place.OpeningHours.Add(new OpeningHoursWindow(DayOfWeek.Monday, 540, 1020)); // 09:00-17:00

        var prompt = PlaceEnrichmentPromptBuilder.Build(place);

        Assert.IsTrue(prompt.Contains("Monday"));
        Assert.IsTrue(prompt.Contains("09:00"));
        Assert.IsTrue(prompt.Contains("17:00"));
    }

    [TestMethod]
    public void Build_WithoutCategoryAttributes_OmitsCategories()
    {
        var place = CreatePlace("Prado Museum");
        place.AddAttribute(new PlaceAttribute("foursquare", "chain", "Prado"));

        var prompt = PlaceEnrichmentPromptBuilder.Build(place);

        Assert.IsFalse(prompt.Contains("Categories:"));
    }

    [TestMethod]
    public void Build_WithTipsText_IncludesTips()
    {
        var place = CreatePlace("Prado Museum");

        var prompt = PlaceEnrichmentPromptBuilder.Build(place, "Great art collection");

        Assert.IsTrue(prompt.Contains("Great art collection"));
        Assert.IsTrue(prompt.Contains("Visitor Tips:"));
    }

    [TestMethod]
    public void Build_WithoutTipsText_DoesNotIncludeTipsSection()
    {
        var place = CreatePlace("Prado Museum");

        var prompt = PlaceEnrichmentPromptBuilder.Build(place);

        Assert.IsFalse(prompt.Contains("Visitor Tips:"));
    }

    [TestMethod]
    public void Build_WithNameContainingQuotes_EscapesQuotes()
    {
        var place = CreatePlace("Museo \"Bellas Artes\"");

        var prompt = PlaceEnrichmentPromptBuilder.Build(place);

        Assert.IsTrue(prompt.Contains("Museo \\\"Bellas Artes\\\""));
    }

    [TestMethod]
    public void Build_WithTipsContainingQuotes_EscapesQuotes()
    {
        var place = CreatePlace("Test Place");

        var prompt = PlaceEnrichmentPromptBuilder.Build(place, "He said \"Great!\"");

        Assert.IsTrue(prompt.Contains("He said \\\"Great!\\\""));
    }

    [TestMethod]
    public void Build_Always_ContainsJsonSchema()
    {
        var place = CreatePlace("Prado Museum");

        var prompt = PlaceEnrichmentPromptBuilder.Build(place);

        Assert.IsTrue(prompt.Contains("TypicalDurationMinutes"));
        Assert.IsTrue(prompt.Contains("IsIndoor"));
        Assert.IsTrue(prompt.Contains("FamilyFriendlyScore"));
        Assert.IsTrue(prompt.Contains("Popularity"));
    }

    private static Place CreatePlace(string name)
    {
        return new Place("fsq-test", name, 1L, new PlaceLocation(40.4168, -3.7038));
    }
}
