using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.Domain.Repository;

public interface ICityRepository : IRepository<City>
{
    Task<City?> GetByCodeAsync(string cityCode);
}
