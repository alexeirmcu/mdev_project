using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Base;
using SmartTripPlanner.Domain.Services;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.Services;

[TestClass]
public sealed class ZoneClusteringHelperTests
{
    private static Place MakePlace(long id, string name, double lat, double lng)
    {
        var place = new Place($"fsq_{id}", name, 1, new PlaceLocation(lat, lng));
        // Use reflection to set Id since it's normally set by EF
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(place, id);
        return place;
    }

    [TestMethod]
    public void Cluster_PlacesWithin2km_SameCluster()
    {
        // Puerta del Sol, Madrid
        var sol = MakePlace(1, "Puerta del Sol", 40.4168, -3.7038);
        // Plaza Mayor, Madrid (~0.3 km)
        var plazaMayor = MakePlace(2, "Plaza Mayor", 40.4154, -3.7074);

        var clusters = ZoneClusteringHelper.Cluster(new List<Place> { sol, plazaMayor });

        Assert.AreEqual(1, clusters.Count);
        Assert.AreEqual(2, clusters[0].Count);
    }

    [TestMethod]
    public void Cluster_PlacesFarApart_DifferentClusters()
    {
        var madrid = MakePlace(1, "Madrid Centro", 40.4168, -3.7038);
        var paris = MakePlace(2, "Paris Centro", 48.8566, 2.3522);

        var clusters = ZoneClusteringHelper.Cluster(new List<Place> { madrid, paris });

        Assert.AreEqual(2, clusters.Count);
    }

    [TestMethod]
    public void Cluster_SinglePlace_SingleCluster()
    {
        var place = MakePlace(1, "Single Place", 40.4168, -3.7038);

        var clusters = ZoneClusteringHelper.Cluster(new List<Place> { place });

        Assert.AreEqual(1, clusters.Count);
        Assert.AreEqual(1, clusters[0].Count);
    }

    [TestMethod]
    public void Cluster_EmptyList_ReturnsEmpty()
    {
        var clusters = ZoneClusteringHelper.Cluster(new List<Place>());

        Assert.AreEqual(0, clusters.Count);
    }

    [TestMethod]
    public void Cluster_MultiplePlacesNearby_AllInSameCluster()
    {
        // Three places within ~200m in central Madrid
        var sol = MakePlace(1, "Puerta del Sol", 40.4168, -3.7038);
        var plazaMayor = MakePlace(2, "Plaza Mayor", 40.4154, -3.7074);
        var palacio = MakePlace(3, "Palacio Real", 40.4180, -3.7140);

        var clusters = ZoneClusteringHelper.Cluster(new List<Place> { sol, plazaMayor, palacio });

        Assert.AreEqual(1, clusters.Count);
        Assert.AreEqual(3, clusters[0].Count);
    }

    [TestMethod]
    public void Cluster_Exactly2kmApart_SameCluster()
    {
        // Use slightly less than 2 km to ensure the boundary value is within
        // the <= ZoneRadiusKm threshold (earth curvature makes exact 2 km tricky)
        var center = MakePlace(1, "Center", 40.4168, -3.7038);
        // ~0.0175° latitude ≈ 1.95 km
        var boundary = MakePlace(2, "Boundary", 40.4343, -3.7038);

        var clusters = ZoneClusteringHelper.Cluster(new List<Place> { center, boundary });

        // Verify the actual distance is under 2 km
        var distance = center.Location.DistanceKmTo(boundary.Location);
        Assert.IsTrue(distance > 1.9, $"Distance {distance:F3} km should be > 1.9 km");
        Assert.IsTrue(distance <= 2.0, $"Distance {distance:F3} km should be <= 2.0 km");

        // Should be in same cluster (distance <= ZoneRadiusKm=2.0)
        Assert.AreEqual(1, clusters.Count);
        Assert.AreEqual(2, clusters[0].Count);
    }

    [TestMethod]
    public void Cluster_JustOver2kmApart_DifferentClusters()
    {
        // Two places at ~2.5 km apart — just over the 2 km threshold
        var center = MakePlace(1, "Center", 40.4168, -3.7038);
        // Place at ~2.5 km north (~0.0225° latitude)
        var far = MakePlace(2, "Far", 40.4393, -3.7038);

        var clusters = ZoneClusteringHelper.Cluster(new List<Place> { center, far });

        // Verify the actual distance is > 2 km
        var distance = center.Location.DistanceKmTo(far.Location);
        Assert.IsTrue(distance > 2.0, $"Distance {distance:F3} km should be > 2 km");

        // Should be in different clusters
        Assert.AreEqual(2, clusters.Count);
    }

    [TestMethod]
    public void Cluster_ThreePlacesTwoClusters_CorrectGrouping()
    {
        // Two nearby (within 2 km) and one far away
        var a = MakePlace(1, "A", 40.4168, -3.7038);
        var b = MakePlace(2, "B", 40.4154, -3.7074); // ~0.3 km from A
        var c = MakePlace(3, "C", 40.4500, -3.6900); // ~3.7 km from A

        var clusters = ZoneClusteringHelper.Cluster(new List<Place> { a, b, c });

        // A and B should be together, C separate
        Assert.AreEqual(2, clusters.Count);
        var clusterWithA = clusters.First(cl => cl.Any(p => p.Id == 1));
        Assert.AreEqual(2, clusterWithA.Count);
        Assert.IsTrue(clusterWithA.Any(p => p.Id == 2));
    }

    [TestMethod]
    public void Cluster_IdenticalLocations_SameCluster()
    {
        var a = MakePlace(1, "Same Spot A", 40.4168, -3.7038);
        var b = MakePlace(2, "Same Spot B", 40.4168, -3.7038);

        var clusters = ZoneClusteringHelper.Cluster(new List<Place> { a, b });

        Assert.AreEqual(1, clusters.Count);
        Assert.AreEqual(2, clusters[0].Count);
    }
}
