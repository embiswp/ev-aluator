using EvAluator.Domain.ValueObjects;

namespace EvAluator.UnitTests.ValueObjects;

public class CoordinatesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(45.5, -122.7)]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public void Create_WithValidCoordinates_ShouldReturnSuccess(double latitude, double longitude)
    {
        var result = Coordinates.Create(latitude, longitude);
        
        Assert.True(result.IsSuccess);
        result.Match(
            coords => 
            {
                Assert.Equal(latitude, coords.Latitude);
                Assert.Equal(longitude, coords.Longitude);
            },
            _ => Assert.Fail("Expected success"));
    }

    [Theory]
    [InlineData(91, 0, "Latitude must be between -90 and 90 degrees")]
    [InlineData(-91, 0, "Latitude must be between -90 and 90 degrees")]
    [InlineData(0, 181, "Longitude must be between -180 and 180 degrees")]
    [InlineData(0, -181, "Longitude must be between -180 and 180 degrees")]
    public void Create_WithInvalidCoordinates_ShouldReturnFailure(double latitude, double longitude, string expectedError)
    {
        var result = Coordinates.Create(latitude, longitude);
        
        Assert.True(result.IsFailure);
        result.Match(
            _ => Assert.Fail("Expected failure"),
            error => Assert.Equal(expectedError, error));
    }

    [Fact]
    public void DistanceTo_BetweenSamePoint_ShouldReturnZero()
    {
        var coords1 = Coordinates.Create(45.5, -122.7).Match(c => c, _ => default);
        var coords2 = Coordinates.Create(45.5, -122.7).Match(c => c, _ => default);
        
        var distance = coords1.DistanceTo(coords2);
        
        Assert.Equal(0, distance, 1);
    }

    [Fact]
    public void DistanceTo_BetweenDifferentPoints_ShouldCalculateCorrectDistance()
    {
        var portland = Coordinates.Create(45.5152, -122.6784).Match(c => c, _ => default);
        var seattle = Coordinates.Create(47.6062, -122.3321).Match(c => c, _ => default);
        
        var distance = portland.DistanceTo(seattle);
        
        Assert.InRange(distance, 230, 250);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var coords = Coordinates.Create(45.123456, -122.654321).Match(c => c, _ => default);
        
        var result = coords.ToString();
        
        Assert.Equal("(45.123456, -122.654321)", result);
    }
}