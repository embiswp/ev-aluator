using EvAluator.Shared.Types;

namespace EvAluator.Domain.ValueObjects;

public readonly record struct Coordinates
{
    public double Latitude { get; }
    public double Longitude { get; }

    private Coordinates(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static Result<Coordinates> Create(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            return Result<Coordinates>.Failure("Latitude must be between -90 and 90 degrees");

        if (longitude < -180 || longitude > 180)
            return Result<Coordinates>.Failure("Longitude must be between -180 and 180 degrees");

        return Result<Coordinates>.Success(new Coordinates(latitude, longitude));
    }

    public double DistanceTo(Coordinates other)
    {
        const double earthRadiusKm = 6371.0;
        const double degToRad = Math.PI / 180.0;

        var dLat = (other.Latitude - Latitude) * degToRad;
        var dLon = (other.Longitude - Longitude) * degToRad;

        var lat1Rad = Latitude * degToRad;
        var lat2Rad = other.Latitude * degToRad;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1Rad) * Math.Cos(lat2Rad);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    public override string ToString() => $"({Latitude:F6}, {Longitude:F6})";
}