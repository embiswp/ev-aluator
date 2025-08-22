using EvAluator.Domain.ValueObjects;
using EvAluator.Shared.Types;

namespace EvAluator.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; private set; }
    public string Make { get; private set; }
    public string Model { get; private set; }
    public int Year { get; private set; }
    public Distance MaxRange { get; private set; }
    public double ChargingSpeedKwh { get; private set; }

    private Vehicle(Guid id, string make, string model, int year, Distance maxRange, double chargingSpeedKwh)
    {
        Id = id;
        Make = make;
        Model = model;
        Year = year;
        MaxRange = maxRange;
        ChargingSpeedKwh = chargingSpeedKwh;
    }

    public static Result<Vehicle> Create(string make, string model, int year, Distance maxRange, double chargingSpeedKwh)
    {
        if (string.IsNullOrWhiteSpace(make))
            return Result<Vehicle>.Failure("Make is required");

        if (string.IsNullOrWhiteSpace(model))
            return Result<Vehicle>.Failure("Model is required");

        if (year < 1900 || year > DateTime.Now.Year + 1)
            return Result<Vehicle>.Failure($"Year must be between 1900 and {DateTime.Now.Year + 1}");

        if (chargingSpeedKwh <= 0)
            return Result<Vehicle>.Failure("Charging speed must be positive");

        return Result<Vehicle>.Success(new Vehicle(
            Guid.NewGuid(),
            make.Trim(),
            model.Trim(),
            year,
            maxRange,
            chargingSpeedKwh));
    }

    public static Vehicle Restore(Guid id, string make, string model, int year, Distance maxRange, double chargingSpeedKwh) =>
        new(id, make, model, year, maxRange, chargingSpeedKwh);

    public BatteryRange CreateBatteryRange(Distance currentRange) =>
        BatteryRange.Create(MaxRange, currentRange)
            .Match(br => br, _ => BatteryRange.Create(MaxRange, Distance.FromKilometers(0).Match(d => d, _ => default))
                .Match(br => br, _ => default));

    public bool CanCompleteTrip(Trip trip, BatteryRange currentBatteryRange) =>
        trip.IsFeasibleWith(currentBatteryRange);

    public override string ToString() => $"{Year} {Make} {Model} (Range: {MaxRange})";
}