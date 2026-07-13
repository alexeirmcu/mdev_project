using Moq;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Infrastructure.LLM;

namespace SmartTripPlanner.Tests.Infrastructure.LLM;

[TestClass]
public sealed class PlaceEnrichmentPromptBuilderTests
{
    private const string DefaultUserTemplate =
        "Place: {{Name}}\n{{CategoriesSection}}{{OpeningHoursSection}}{{VisitorTipsSection}}\n\nRespond with valid JSON only in this exact schema:\n{{Schema}}";

    private static PlaceEnrichmentPromptBuilder CreateBuilder(string userTemplate)
    {
        var mock = new Mock<IPromptTemplateProvider>();
        mock.Setup(t => t.GetTemplate("PlaceEnrichment"))
            .Returns(new PromptTemplate("system", userTemplate));
        return new PlaceEnrichmentPromptBuilder(mock.Object);
    }

    [TestMethod]
    public void Build_WithPlace_ContainsName()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Prado Museum");

        var prompt = builder.Build(place, null);

        Assert.IsTrue(prompt.Contains("Prado Museum"));
    }

    [TestMethod]
    public void Build_WithPlace_ContainsCategories()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Prado Museum");
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "museum"));
        place.AddAttribute(new PlaceAttribute("foursquare", "category", "art"));

        var prompt = builder.Build(place, null);

        Assert.IsTrue(prompt.Contains("museum, art") || prompt.Contains("art, museum"));
    }

    [TestMethod]
    public void Build_WithPlace_ContainsOpeningHours()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Prado Museum");
        place.OpeningHours.Add(new OpeningHoursWindow(DayOfWeek.Monday, 540, 1020)); // 09:00-17:00

        var prompt = builder.Build(place, null);

        Assert.IsTrue(prompt.Contains("Monday"));
        Assert.IsTrue(prompt.Contains("09:00"));
        Assert.IsTrue(prompt.Contains("17:00"));
    }

    [TestMethod]
    public void Build_WithoutCategoryAttributes_OmitsCategories()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Prado Museum");
        place.AddAttribute(new PlaceAttribute("foursquare", "chain", "Prado"));

        var prompt = builder.Build(place, null);

        Assert.IsFalse(prompt.Contains("Categories:"));
    }

    [TestMethod]
    public void Build_WithTipsText_IncludesTips()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Prado Museum");

        var prompt = builder.Build(place, "Great art collection");

        Assert.IsTrue(prompt.Contains("Great art collection"));
        Assert.IsTrue(prompt.Contains("Visitor Tips:"));
    }

    [TestMethod]
    public void Build_WithoutTipsText_DoesNotIncludeTipsSection()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Prado Museum");

        var prompt = builder.Build(place, null);

        Assert.IsFalse(prompt.Contains("Visitor Tips:"));
    }

    [TestMethod]
    public void Build_WithNameContainingQuotes_EscapesQuotes()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Museo \"Bellas Artes\"");

        var prompt = builder.Build(place, null);

        Assert.IsTrue(prompt.Contains("Museo \\\"Bellas Artes\\\""));
    }

    [TestMethod]
    public void Build_WithTipsContainingQuotes_EscapesQuotes()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Test Place");

        var prompt = builder.Build(place, "He said \"Great!\"");

        Assert.IsTrue(prompt.Contains("He said \\\"Great!\\\""));
    }

    [TestMethod]
    public void Build_Always_ContainsJsonSchema()
    {
        var builder = CreateBuilder(DefaultUserTemplate);
        var place = CreatePlace("Prado Museum");

        var prompt = builder.Build(place, null);

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
