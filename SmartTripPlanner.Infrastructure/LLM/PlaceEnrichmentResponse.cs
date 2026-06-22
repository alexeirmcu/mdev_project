namespace SmartTripPlanner.Infrastructure.LLM;

internal sealed class PlaceEnrichmentResponse
{
    public int TypicalDurationMinutes { get; set; }
    public bool IsIndoor { get; set; }
    public int FamilyFriendlyScore { get; set; }
    public double Popularity { get; set; }

    public void Validate()
    {
        if (TypicalDurationMinutes < 15 || TypicalDurationMinutes > 480)
            throw new InvalidOperationException($"TypicalDurationMinutes must be 15-480. Got {TypicalDurationMinutes}.");
        if (FamilyFriendlyScore < 1 || FamilyFriendlyScore > 5)
            throw new InvalidOperationException($"FamilyFriendlyScore must be 1-5. Got {FamilyFriendlyScore}.");
        if (Popularity < 0.0 || Popularity > 1.0)
            throw new InvalidOperationException($"Popularity must be 0.0-1.0. Got {Popularity}.");
    }
}
