using EvAluator.Shared.Types;

namespace EvAluator.Domain.ValueObjects;

public readonly record struct BatteryRange
{
    public Distance MaxRange { get; }
    public Distance CurrentRange { get; }

    private BatteryRange(Distance maxRange, Distance currentRange)
    {
        MaxRange = maxRange;
        CurrentRange = currentRange;
    }

    public static Result<BatteryRange> Create(Distance maxRange, Distance currentRange)
    {
        if (currentRange > maxRange)
            return Result<BatteryRange>.Failure("Current range cannot exceed maximum range");

        return Result<BatteryRange>.Success(new BatteryRange(maxRange, currentRange));
    }

    public double ChargePercentage => MaxRange.Kilometers > 0 ? CurrentRange.Kilometers / MaxRange.Kilometers * 100 : 0;

    public bool CanReach(Distance requiredDistance) => CurrentRange >= requiredDistance;

    public Distance RemainingAfterTrip(Distance tripDistance) => 
        CurrentRange >= tripDistance ? CurrentRange - tripDistance : Distance.FromKilometers(0).Match(d => d, _ => default);

    public override string ToString() => 
        $"{CurrentRange} / {MaxRange} ({ChargePercentage:F1}%)";
}