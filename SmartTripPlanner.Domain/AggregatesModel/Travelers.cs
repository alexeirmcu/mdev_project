using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.AggregatesModel;

public class Travelers : ValueObject
{
    private const int MinAdults = 1;
    private const int MaxTotal = 10;

    public int Adults { get; }
    public int Children { get; }
    public int Infants { get; }

    public int Total => Adults + Children + Infants;

    public Travelers(int adults, int children = 0, int infants = 0)
    {
        if (adults < MinAdults)
            throw new ArgumentException($"Adults must be at least {MinAdults}.", nameof(adults));
        if (children < 0)
            throw new ArgumentException("Children cannot be negative.", nameof(children));
        if (infants < 0)
            throw new ArgumentException("Infants cannot be negative.", nameof(infants));

        var total = adults + children + infants;
        if (total > MaxTotal)
            throw new ArgumentException($"Total travelers ({total}) exceeds maximum allowed ({MaxTotal}).", nameof(adults));

        Adults = adults;
        Children = children;
        Infants = infants;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Adults;
        yield return Children;
        yield return Infants;
    }
}
