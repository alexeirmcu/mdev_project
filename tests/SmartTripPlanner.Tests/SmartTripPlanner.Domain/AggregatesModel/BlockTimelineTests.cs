using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Enums;

namespace SmartTripPlanner.Tests.Domain.AggregatesModel;

[TestClass]
public sealed class BlockTimelineTests
{
    private static BlockTimeline CreateMorningBlock() => new()
    {
        BlockType = BlockType.Morning
    };

    private static BlockTimeline CreateEveningBlock() => new()
    {
        BlockType = BlockType.Evening
    };

    private static ActivityNode CreateActivity(int durationMinutes, int transitMinutes = 0) => new()
    {
        PlaceId = 1L,
        Name = "Test Activity",
        SequenceOrder = 1,
        DurationMinutes = durationMinutes,
        IsIndoor = false,
        TransitToNext = transitMinutes > 0
            ? new TransitDetails(TransportMode.WALK_AND_PUBLIC_TRANSPORT, transitMinutes, 5, false)
            : null
    };

    [TestMethod]
    public void AddActivity_WithinCapacity_AddsActivity()
    {
        var block = CreateMorningBlock();
        var activity = CreateActivity(60);

        block.AddActivity(activity);

        Assert.AreEqual(1, block.Activities.Count);
        Assert.AreEqual(activity, block.Activities[0]);
    }

    [TestMethod]
    public void AddActivity_ExceedsMaxVisits_ThrowsException()
    {
        var block = CreateEveningBlock();
        // Evening max is 2 visits
        block.AddActivity(CreateActivity(30));
        block.AddActivity(CreateActivity(30));

        try
        {
            block.AddActivity(CreateActivity(30));
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "maximum visits");
        }
    }

    [TestMethod]
    public void AddActivity_ExceedsDuration_ThrowsException()
    {
        var block = CreateMorningBlock();
        // Morning max is 210 min. Add an activity that exceeds it.
        var activity = CreateActivity(220);

        try
        {
            block.AddActivity(activity);
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "exceeds maximum duration");
        }
    }

    [TestMethod]
    public void RemoveActivity_RemovesFromList()
    {
        var block = CreateMorningBlock();
        var activity1 = CreateActivity(60);
        var activity2 = CreateActivity(45);

        block.AddActivity(activity1);
        block.AddActivity(activity2);
        Assert.AreEqual(2, block.Activities.Count);

        block.RemoveActivity(activity1);
        Assert.AreEqual(1, block.Activities.Count);
        Assert.AreEqual(activity2, block.Activities[0]);
    }

    [TestMethod]
    public void CanFitActivity_ReturnsTrue_WhenSpaceAvailable()
    {
        var block = CreateMorningBlock();
        // Morning: 210 min max, 3 visits max
        bool fits = block.CanFitActivity(90);
        Assert.IsTrue(fits);
    }

    [TestMethod]
    public void CanFitActivity_ReturnsFalse_WhenNoSpace()
    {
        var block = CreateEveningBlock();
        block.AddActivity(CreateActivity(60));
        block.AddActivity(CreateActivity(30));
        // Evening max is 2 visits, third won't fit
        bool fits = block.CanFitActivity(15);
        Assert.IsFalse(fits);
    }

    [TestMethod]
    public void CanFitActivity_ReturnsFalse_WhenDurationExceeds()
    {
        var block = CreateMorningBlock();
        // Morning max is 210 min. Adding 220 won't fit.
        bool fits = block.CanFitActivity(220);
        Assert.IsFalse(fits);
    }

    [TestMethod]
    public void BlockTotalDurationMinutes_IsComputedFromActivities()
    {
        var block = CreateMorningBlock();
        var activity1 = CreateActivity(60, 10); // 60 + 10 transit
        var activity2 = CreateActivity(45, 5);  // 45 + 5 transit

        block.AddActivity(activity1);
        block.AddActivity(activity2);

        // 60 + 10 + 45 + 5 = 120
        Assert.AreEqual(120, block.BlockTotalDurationMinutes);
    }

    [TestMethod]
    public void BlockTotalDurationMinutes_IsZero_WhenNoActivities()
    {
        var block = CreateMorningBlock();
        Assert.AreEqual(0, block.BlockTotalDurationMinutes);
    }

    [TestMethod]
    public void BlockTotalDurationMinutes_ExcludesHotelTransit()
    {
        var block = CreateMorningBlock();
        block.TransitFromHotel = new TransitDetails(TransportMode.WALK_AND_PUBLIC_TRANSPORT, 15, 5, false);
        block.TransitToHotel = new TransitDetails(TransportMode.WALK_AND_PUBLIC_TRANSPORT, 20, 5, false);
        var activity = CreateActivity(60);
        block.AddActivity(activity);

        // Activity duration only (60), not hotel transit
        Assert.AreEqual(60, block.BlockTotalDurationMinutes);
    }

    [TestMethod]
    public void BlockWallClockDurationMinutes_IncludesHotelTransit()
    {
        var block = CreateMorningBlock();
        block.TransitFromHotel = new TransitDetails(TransportMode.WALK_AND_PUBLIC_TRANSPORT, 15, 5, false);
        block.TransitToHotel = new TransitDetails(TransportMode.WALK_AND_PUBLIC_TRANSPORT, 20, 5, false);
        var activity = CreateActivity(60);
        block.AddActivity(activity);

        // 15 (from hotel) + 60 (activity) + 20 (to hotel) = 95
        Assert.AreEqual(95, block.BlockWallClockDurationMinutes);
    }

    [TestMethod]
    public void BlockWallClockDurationMinutes_Zero_WhenNoHotelTransitAndNoActivities()
    {
        var block = CreateMorningBlock();
        Assert.AreEqual(0, block.BlockWallClockDurationMinutes);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // InterBlockTransit tests
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void InterBlockTransit_DefaultIsNull()
    {
        var block = CreateMorningBlock();
        Assert.IsNull(block.InterBlockTransit);
    }

    [TestMethod]
    public void InterBlockTransit_CanBeSet()
    {
        var block = CreateMorningBlock();
        var transit = new TransitDetails(TransportMode.WALK_AND_PUBLIC_TRANSPORT, 10, 5, false);
        block.InterBlockTransit = transit;
        Assert.IsNotNull(block.InterBlockTransit);
        Assert.AreEqual(10, block.InterBlockTransit.DurationMinutes);
    }

    [TestMethod]
    public void BlockTotalDurationMinutes_ExcludesInterBlockTransit()
    {
        var block = CreateMorningBlock();
        block.InterBlockTransit = new TransitDetails(TransportMode.WALK_AND_PUBLIC_TRANSPORT, 15, 5, false);
        var activity = CreateActivity(60);
        block.AddActivity(activity);

        // Inter-block transit is external to block capacity — should not affect TotalDuration
        Assert.AreEqual(60, block.BlockTotalDurationMinutes);
    }
}
