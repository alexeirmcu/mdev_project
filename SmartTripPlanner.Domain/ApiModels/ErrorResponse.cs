namespace SmartTripPlanner.Domain.ApiModels;

public record ErrorResponse(string Code, string Message, IReadOnlyList<string> ConflictingPlaceIds);
