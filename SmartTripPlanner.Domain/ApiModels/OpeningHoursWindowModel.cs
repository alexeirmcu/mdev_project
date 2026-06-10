namespace SmartTripPlanner.Domain.ApiModels;

public record OpeningHoursWindowModel(DayOfWeek DayOfWeek, int OpenMinutes, int CloseMinutes);
