using EvAluator.Domain.Entities;
using EvAluator.Domain.ValueObjects;

namespace EvAluator.UnitTests.Entities;

public class VehicleTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnSuccess()
    {
        var maxRange = Distance.FromKilometers(400).Match(d => d, _ => default);
        
        var result = Vehicle.Create("Tesla", "Model 3", 2023, maxRange, 150);
        
        Assert.True(result.IsSuccess);
        result.Match(
            vehicle =>
            {
                Assert.Equal("Tesla", vehicle.Make);
                Assert.Equal("Model 3", vehicle.Model);
                Assert.Equal(2023, vehicle.Year);
                Assert.Equal(maxRange, vehicle.MaxRange);
                Assert.Equal(150, vehicle.ChargingSpeedKwh);
            },
            _ => Assert.Fail("Expected success"));
    }

    [Theory]
    [InlineData("", "Model 3", 2023, "Make is required")]
    [InlineData("Tesla", "", 2023, "Model is required")]
    [InlineData("Tesla", "Model 3", 1850, "Year must be between 1900 and")]
    [InlineData("Tesla", "Model 3", 2050, "Year must be between 1900 and")]
    public void Create_WithInvalidData_ShouldReturnFailure(string make, string model, int year, string expectedErrorStart)
    {
        var maxRange = Distance.FromKilometers(400).Match(d => d, _ => default);
        
        var result = Vehicle.Create(make, model, year, maxRange, 150);
        
        Assert.True(result.IsFailure);
        result.Match(
            _ => Assert.Fail("Expected failure"),
            error => Assert.StartsWith(expectedErrorStart, error));
    }

    [Fact]
    public void Create_WithNegativeChargingSpeed_ShouldReturnFailure()
    {
        var maxRange = Distance.FromKilometers(400).Match(d => d, _ => default);
        
        var result = Vehicle.Create("Tesla", "Model 3", 2023, maxRange, -10);
        
        Assert.True(result.IsFailure);
        result.Match(
            _ => Assert.Fail("Expected failure"),
            error => Assert.Equal("Charging speed must be positive", error));
    }

    [Fact]
    public void CreateBatteryRange_ShouldReturnValidBatteryRange()
    {
        var maxRange = Distance.FromKilometers(400).Match(d => d, _ => default);
        var currentRange = Distance.FromKilometers(300).Match(d => d, _ => default);
        var vehicle = Vehicle.Create("Tesla", "Model 3", 2023, maxRange, 150).Match(v => v, _ => default!);
        
        var batteryRange = vehicle.CreateBatteryRange(currentRange);
        
        Assert.Equal(maxRange, batteryRange.MaxRange);
        Assert.Equal(currentRange, batteryRange.CurrentRange);
        Assert.Equal(75, batteryRange.ChargePercentage);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var maxRange = Distance.FromKilometers(400).Match(d => d, _ => default);
        var vehicle = Vehicle.Create("Tesla", "Model 3", 2023, maxRange, 150).Match(v => v, _ => default!);
        
        var result = vehicle.ToString();
        
        Assert.Equal("2023 Tesla Model 3 (Range: 400.00 km)", result);
    }
}