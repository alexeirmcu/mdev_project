namespace SmartTripPlanner.Domain.ApiModels;

public record ActivityCompletionRequest(
    long PlaceId,
    bool IsCompleted);
