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
}
