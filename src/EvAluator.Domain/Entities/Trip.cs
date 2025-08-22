using EvAluator.Domain.ValueObjects;
using EvAluator.Shared.Types;

namespace EvAluator.Domain.Entities;

public class Trip
{
    public Guid Id { get; private set; }
    public LocationHistory StartLocation { get; private set; }
    public LocationHistory EndLocation { get; private set; }
    public Distance Distance { get; private set; }
    public TimeSpan Duration { get; private set; }

    private Trip(Guid id, LocationHistory startLocation, LocationHistory endLocation, Distance distance, TimeSpan duration)
    {
        Id = id;
        StartLocation = startLocation;
        EndLocation = endLocation;
        Distance = distance;
        Duration = duration;
    }

    public static Result<Trip> Create(LocationHistory startLocation, LocationHistory endLocation)
    {
        if (startLocation.Timestamp >= endLocation.Timestamp)
            return Result<Trip>.Failure("End location must be after start location");

        var distance = startLocation.DistanceTo(endLocation);
        var duration = startLocation.TimeTo(endLocation);

        return Result<Trip>.Success(new Trip(
            Guid.NewGuid(), 
            startLocation, 
            endLocation, 
            distance, 
            duration));
    }

    public static Trip Restore(Guid id, LocationHistory startLocation, LocationHistory endLocation, Distance distance, TimeSpan duration) =>
        new(id, startLocation, endLocation, distance, duration);

    public double AverageSpeedKmh => Duration.TotalHours > 0 ? Distance.Kilometers / Duration.TotalHours : 0;

    public bool IsFeasibleWith(BatteryRange batteryRange) => batteryRange.CanReach(Distance);

    public Distance RemainingRangeAfter(BatteryRange batteryRange) => 
        batteryRange.RemainingAfterTrip(Distance);

    public override string ToString() => 
        $"Trip {Distance} from {StartLocation.LocationName ?? "Unknown"} to {EndLocation.LocationName ?? "Unknown"} ({Duration:hh\\:mm})";
}