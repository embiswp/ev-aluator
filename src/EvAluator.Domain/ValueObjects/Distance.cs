using EvAluator.Shared.Types;

namespace EvAluator.Domain.ValueObjects;

public readonly record struct Distance
{
    public double Kilometers { get; }

    private Distance(double kilometers)
    {
        Kilometers = kilometers;
    }

    public static Result<Distance> FromKilometers(double kilometers)
    {
        if (kilometers < 0)
            return Result<Distance>.Failure("Distance cannot be negative");

        return Result<Distance>.Success(new Distance(kilometers));
    }

    public static Result<Distance> FromMiles(double miles)
    {
        if (miles < 0)
            return Result<Distance>.Failure("Distance cannot be negative");

        return Result<Distance>.Success(new Distance(miles * 1.60934));
    }

    public double Miles => Kilometers / 1.60934;

    public static Distance operator +(Distance left, Distance right) =>
        new(left.Kilometers + right.Kilometers);

    public static Distance operator -(Distance left, Distance right) =>
        new(Math.Max(0, left.Kilometers - right.Kilometers));

    public static bool operator <(Distance left, Distance right) =>
        left.Kilometers < right.Kilometers;

    public static bool operator >(Distance left, Distance right) =>
        left.Kilometers > right.Kilometers;

    public static bool operator <=(Distance left, Distance right) =>
        left.Kilometers <= right.Kilometers;

    public static bool operator >=(Distance left, Distance right) =>
        left.Kilometers >= right.Kilometers;

    public override string ToString() => $"{Kilometers:F2} km";
}