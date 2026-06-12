namespace SmartTripPlanner.Domain.ApiModels;

public enum ErrorCode
{
    MIN_LENGTH_VIOLATION,
    INVALID_CITY,
    MAX_RESULTS_EXCEEDED,
    EXTERNAL_SERVICE_FAILURE,
    REQUIRED_FIELD,
    VALIDATION_ERROR
}
