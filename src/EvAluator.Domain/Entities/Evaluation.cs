using EvAluator.Domain.ValueObjects;
using EvAluator.Shared.Types;

namespace EvAluator.Domain.Entities;

public class Evaluation
{
    public Guid Id { get; private set; }
    public Vehicle Vehicle { get; private set; }
    public IReadOnlyList<Trip> Trips { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public EvaluationResult Result { get; private set; }

    private Evaluation(Guid id, Vehicle vehicle, IReadOnlyList<Trip> trips, DateTime createdAt, EvaluationResult result)
    {
        Id = id;
        Vehicle = vehicle;
        Trips = trips;
        CreatedAt = createdAt;
        Result = result;
    }

    public static Result<Evaluation> Create(Vehicle vehicle, IReadOnlyList<Trip> trips)
    {
        if (trips.Count == 0)
            return Result<Evaluation>.Failure("At least one trip is required for evaluation");

        var result = EvaluateTrips(vehicle, trips);
        
        return Result<Evaluation>.Success(new Evaluation(
            Guid.NewGuid(),
            vehicle,
            trips,
            DateTime.UtcNow,
            result));
    }

    public static Evaluation Restore(Guid id, Vehicle vehicle, IReadOnlyList<Trip> trips, DateTime createdAt, EvaluationResult result) =>
        new(id, vehicle, trips, createdAt, result);

    private static EvaluationResult EvaluateTrips(Vehicle vehicle, IReadOnlyList<Trip> trips)
    {
        var totalTrips = trips.Count;
        var feasibleTrips = 0;
        var batteryRange = vehicle.CreateBatteryRange(vehicle.MaxRange);

        var tripResults = trips
            .Select(trip => 
            {
                var isFeasible = trip.IsFeasibleWith(batteryRange);
                if (isFeasible) feasibleTrips++;
                
                return new TripEvaluationResult(
                    trip,
                    isFeasible,
                    trip.RemainingRangeAfter(batteryRange));
            })
            .ToList();

        var feasibilityPercentage = totalTrips > 0 ? (double)feasibleTrips / totalTrips * 100 : 0;

        return new EvaluationResult(
            feasibilityPercentage >= 80,
            feasibilityPercentage,
            tripResults);
    }

    public override string ToString() =>
        $"Evaluation for {Vehicle} - {Result.FeasibilityPercentage:F1}% feasible ({Result.TripResults.Count} trips)";
}

public record EvaluationResult(
    bool IsRecommended,
    double FeasibilityPercentage,
    IReadOnlyList<TripEvaluationResult> TripResults);

public record TripEvaluationResult(
    Trip Trip,
    bool IsFeasible,
    Distance RemainingRange);