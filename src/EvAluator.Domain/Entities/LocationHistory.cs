using EvAluator.Domain.ValueObjects;

namespace EvAluator.Domain.Entities;

public class LocationHistory
{
    public Guid Id { get; private set; }
    public Coordinates Location { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string? LocationName { get; private set; }

    private LocationHistory(Guid id, Coordinates location, DateTime timestamp, string? locationName = null)
    {
        Id = id;
        Location = location;
        Timestamp = timestamp;
        LocationName = locationName;
    }

    public static LocationHistory Create(Coordinates location, DateTime timestamp, string? locationName = null) =>
        new(Guid.NewGuid(), location, timestamp, locationName);

    public static LocationHistory Restore(Guid id, Coordinates location, DateTime timestamp, string? locationName = null) =>
        new(id, location, timestamp, locationName);

    public Distance DistanceTo(LocationHistory other) => 
        Distance.FromKilometers(Location.DistanceTo(other.Location))
            .Match(d => d, _ => Distance.FromKilometers(0).Match(d => d, _ => default));

    public TimeSpan TimeTo(LocationHistory other) => 
        other.Timestamp - Timestamp;

    public override string ToString() => 
        $"{LocationName ?? "Unknown"} at {Location} on {Timestamp:yyyy-MM-dd HH:mm}";
}