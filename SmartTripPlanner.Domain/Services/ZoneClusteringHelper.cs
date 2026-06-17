using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Constants;

namespace SmartTripPlanner.Domain.Services;

/// <summary>
/// Groups places into geographic clusters using haversine distance.
/// Each cluster has a centroid that is updated via running average.
/// </summary>
public static class ZoneClusteringHelper
{
    /// <summary>
    /// Clusters places so that every place in a cluster is within
    /// <see cref="TripPlanningConstants.ZoneRadiusKm"/> km of the cluster centroid.
    /// </summary>
    /// <param name="places">The places to cluster.</param>
    /// <returns>A list of clusters, each being a list of places.</returns>
    public static List<List<Place>> Cluster(IReadOnlyList<Place> places)
    {
        var clusters = new List<List<Place>>();
        var centroids = new List<(double lat, double lng)>();

        foreach (var place in places)
        {
            if (place.Location is null)
                continue;

            bool added = false;
            for (int i = 0; i < clusters.Count; i++)
            {
                var (centroidLat, centroidLng) = centroids[i];
                var centroid = new PlaceLocation(centroidLat, centroidLng);
                var distance = place.Location.DistanceKmTo(centroid);

                if (distance <= TripPlanningConstants.ZoneRadiusKm)
                {
                    clusters[i].Add(place);
                    // Update centroid via running average
                    var count = clusters[i].Count;
                    centroids[i] = (
                        centroidLat + (place.Location.Latitude - centroidLat) / count,
                        centroidLng + (place.Location.Longitude - centroidLng) / count
                    );
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                clusters.Add(new List<Place> { place });
                centroids.Add((place.Location.Latitude, place.Location.Longitude));
            }
        }

        return clusters;
    }
}
