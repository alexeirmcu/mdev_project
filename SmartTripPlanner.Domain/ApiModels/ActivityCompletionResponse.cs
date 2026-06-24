namespace SmartTripPlanner.Domain.ApiModels;

public record ActivityCompletionResponse(
    long PlaceId,
    bool IsCompleted,
    int CompletedCount);
