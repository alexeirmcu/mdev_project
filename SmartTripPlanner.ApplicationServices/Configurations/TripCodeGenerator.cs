using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Configurations;

public interface ITripCodeGenerator
{
    Task<string> GenerateAsync(string cityCode, int year, CancellationToken ct);
}

public class TripCodeGenerator : ITripCodeGenerator
{
    private static readonly Random Random = new();
    private const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly ITripRepository _tripRepository;

    public TripCodeGenerator(ITripRepository tripRepository)
    {
        _tripRepository = tripRepository;
    }

    public async Task<string> GenerateAsync(string cityCode, int year, CancellationToken ct)
    {
        var prefix = $"{cityCode.ToUpperInvariant()}-{year}-";
        string tripCode;

        do
        {
            var randomPart = new string(Enumerable.Repeat(Chars, 4)
                .Select(s => s[Random.Next(s.Length)]).ToArray());
            tripCode = prefix + randomPart;
        } while (await _tripRepository.ExistsByTripCodeAsync(tripCode, ct));

        return tripCode;
    }
}
