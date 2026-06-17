namespace SmartTripPlanner.Domain.ApiModels;

public record TravelersInput(
    int Adults = 2,
    int Children = 0,
    int Infants = 0);
