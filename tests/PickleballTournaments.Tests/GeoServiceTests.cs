using PickleballTournaments.Api.Services;
using Xunit;

namespace PickleballTournaments.Tests;

public class GeoServiceTests
{
    [Fact]
    public void DistanceMiles_HoustonToDallas_IsApproximatelyCorrect()
    {
        // Known great-circle distance ~225 miles.
        var distance = GeoService.DistanceMiles(29.7604, -95.3698, 32.7767, -96.7970);
        Assert.InRange(distance, 220, 230);
    }

    [Fact]
    public void DistanceMiles_SamePoint_IsZero()
    {
        var distance = GeoService.DistanceMiles(29.7604, -95.3698, 29.7604, -95.3698);
        Assert.Equal(0, distance, precision: 6);
    }

    [Fact]
    public void BoundingBoxFor_ContainsCenterPoint()
    {
        var box = GeoService.BoundingBoxFor(29.7604, -95.3698, 100);
        Assert.True(box.SwLat < 29.7604 && 29.7604 < box.NeLat);
        Assert.True(box.SwLng < -95.3698 && -95.3698 < box.NeLng);
    }
}
