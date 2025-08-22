using EvAluator.Domain.ValueObjects;

namespace EvAluator.UnitTests.ValueObjects;

public class DistanceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(1000.5)]
    public void FromKilometers_WithValidDistance_ShouldReturnSuccess(double kilometers)
    {
        var result = Distance.FromKilometers(kilometers);
        
        Assert.True(result.IsSuccess);
        result.Match(
            distance => Assert.Equal(kilometers, distance.Kilometers),
            _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public void FromKilometers_WithNegativeDistance_ShouldReturnFailure()
    {
        var result = Distance.FromKilometers(-10);
        
        Assert.True(result.IsFailure);
        result.Match(
            _ => Assert.Fail("Expected failure"),
            error => Assert.Equal("Distance cannot be negative", error));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(62.137)]
    [InlineData(100)]
    public void FromMiles_WithValidDistance_ShouldReturnSuccess(double miles)
    {
        var result = Distance.FromMiles(miles);
        
        Assert.True(result.IsSuccess);
        result.Match(
            distance => Assert.Equal(miles * 1.60934, distance.Kilometers, 3),
            _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public void Miles_ShouldConvertFromKilometersCorrectly()
    {
        var distance = Distance.FromKilometers(100).Match(d => d, _ => default);
        
        var miles = distance.Miles;
        
        Assert.Equal(62.137, miles, 3);
    }

    [Fact]
    public void Addition_ShouldCombineDistances()
    {
        var distance1 = Distance.FromKilometers(50).Match(d => d, _ => default);
        var distance2 = Distance.FromKilometers(30).Match(d => d, _ => default);
        
        var result = distance1 + distance2;
        
        Assert.Equal(80, result.Kilometers);
    }

    [Fact]
    public void Subtraction_ShouldReturnDifference()
    {
        var distance1 = Distance.FromKilometers(100).Match(d => d, _ => default);
        var distance2 = Distance.FromKilometers(30).Match(d => d, _ => default);
        
        var result = distance1 - distance2;
        
        Assert.Equal(70, result.Kilometers);
    }

    [Fact]
    public void Subtraction_WithResultBelowZero_ShouldReturnZero()
    {
        var distance1 = Distance.FromKilometers(30).Match(d => d, _ => default);
        var distance2 = Distance.FromKilometers(100).Match(d => d, _ => default);
        
        var result = distance1 - distance2;
        
        Assert.Equal(0, result.Kilometers);
    }

    [Theory]
    [InlineData(50, 100, true)]
    [InlineData(100, 50, false)]
    [InlineData(75, 75, false)]
    public void LessThan_ShouldCompareCorrectly(double km1, double km2, bool expected)
    {
        var distance1 = Distance.FromKilometers(km1).Match(d => d, _ => default);
        var distance2 = Distance.FromKilometers(km2).Match(d => d, _ => default);
        
        var result = distance1 < distance2;
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var distance = Distance.FromKilometers(123.456).Match(d => d, _ => default);
        
        var result = distance.ToString();
        
        Assert.Equal("123.46 km", result);
    }
}