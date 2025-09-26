using System.ComponentModel.DataAnnotations;

namespace EVRangeAnalyzer.Models;

/// <summary>
/// Represents aggregated daily driving distances calculated from location points.
/// Used for EV range compatibility analysis and performance metrics.
/// Cached for performance during repeated analysis requests.
/// </summary>
public class DailyTripSummary
{
    /// <summary>
    /// Gets or sets the session ID that owns this daily summary.
    /// Foreign key reference to UserSession for data isolation.
    /// </summary>
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the analysis date for this summary (local timezone).
    /// Represents one calendar day of driving activity.
    /// </summary>
    [Required]
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the total distance driven in motorized vehicles on this date.
    /// Calculated by summing all qualifying trips (excludes walking, cycling, etc.).
    /// </summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Total distance must be non-negative")]
    public decimal TotalDistanceKm { get; set; }

    /// <summary>
    /// Gets or sets the number of separate motorized vehicle trips on this date.
    /// A trip is defined as continuous vehicle movement with stops &lt; 15 minutes.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Motorized trips count must be non-negative")]
    public int MotorizedTrips { get; set; }

    /// <summary>
    /// Gets or sets the distance of the longest single vehicle trip on this date.
    /// Used to identify days that might challenge single-charge EV capability.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Longest trip distance must be non-negative")]
    public decimal LongestTripKm { get; set; }

    /// <summary>
    /// Gets or sets the average speed across all motorized trips on this date.
    /// Calculated as total distance divided by total driving time (excluding stops).
    /// </summary>
    [Range(0, 200, ErrorMessage = "Average speed must be between 0 and 200 km/h")]
    public decimal AverageSpeedKmh { get; set; }

    /// <summary>
    /// Gets or sets the transport modes detected during motorized trips on this date.
    /// Used to understand the variety of vehicle types used.
    /// </summary>
    public List<TransportMode> TransportModes { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when this summary was calculated (UTC).
    /// Used for cache invalidation and debugging purposes.
    /// </summary>
    [Required]
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the number of location points used in this calculation.
    /// Used for data quality assessment and debugging.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Location points count must be non-negative")]
    public int LocationPointsUsed { get; set; }

    /// <summary>
    /// Gets or sets the total driving time in minutes for this date.
    /// Excludes stationary periods and non-motorized transport.
    /// </summary>
    [Range(0, 1440, ErrorMessage = "Driving time must be between 0 and 1440 minutes (24 hours)")]
    public int DrivingTimeMinutes { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about trip calculations.
    /// Stores information about data quality, gaps, and processing notes.
    /// </summary>
    public Dictionary<string, object> CalculationMetadata { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DailyTripSummary"/> class.
    /// </summary>
    public DailyTripSummary()
    {
        CalculatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new DailyTripSummary from a collection of location points for a specific date.
    /// </summary>
    /// <param name="sessionId">The session ID that owns this summary.</param>
    /// <param name="date">The date to analyze.</param>
    /// <param name="locationPoints">Location points for this date, filtered to motorized transport.</param>
    /// <returns>A calculated daily trip summary with aggregated metrics.</returns>
    public static DailyTripSummary CalculateFromLocationPoints(
        string sessionId,
        DateOnly date,
        IEnumerable<LocationPoint> locationPoints)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        var points = locationPoints?.Where(p => p.ShouldIncludeInAnalysis()).OrderBy(p => p.Timestamp).ToList()
                    ?? throw new ArgumentNullException(nameof(locationPoints));

        var summary = new DailyTripSummary
        {
            SessionId = sessionId,
            Date = date,
            LocationPointsUsed = points.Count,
        };

        if (!points.Any())
        {
            return summary; // Return empty summary for days with no qualifying data
        }

        // Calculate trips by grouping consecutive points with similar transport modes
        var trips = IdentifyTrips(points);

        // Aggregate trip metrics
        summary.MotorizedTrips = trips.Count;
        summary.TotalDistanceKm = trips.Sum(trip => trip.DistanceKm);
        summary.LongestTripKm = trips.Any() ? trips.Max(trip => trip.DistanceKm) : 0;
        summary.DrivingTimeMinutes = trips.Sum(trip => trip.DurationMinutes);

        // Calculate average speed (total distance / total time)
        if (summary.DrivingTimeMinutes > 0)
        {
            summary.AverageSpeedKmh = (summary.TotalDistanceKm * 60) / summary.DrivingTimeMinutes;
        }

        // Extract unique transport modes
        summary.TransportModes = points.Select(p => p.ActivityType)
                                      .Where(mode => mode.IsIncludedInAnalysis())
                                      .Distinct()
                                      .ToList();

        // Store calculation metadata
        summary.CalculationMetadata["TotalTrips"] = trips.Count;
        summary.CalculationMetadata["ProcessingTime"] = DateTime.UtcNow;
        summary.CalculationMetadata["DataQuality"] = CalculateDataQuality(points);

        return summary;
    }

    /// <summary>
    /// Validates the current daily trip summary for logical consistency.
    /// </summary>
    /// <returns>A list of validation error messages, or empty list if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Required field validation
        if (string.IsNullOrWhiteSpace(SessionId))
            errors.Add("Session ID is required");

        // Distance validations
        if (TotalDistanceKm < 0)
            errors.Add("Total distance must be non-negative");

        if (LongestTripKm < 0)
            errors.Add("Longest trip distance must be non-negative");

        if (LongestTripKm > TotalDistanceKm)
            errors.Add("Longest trip distance cannot exceed total daily distance");

        // Count validations
        if (MotorizedTrips < 0)
            errors.Add("Motorized trips count must be non-negative");

        if (LocationPointsUsed < 0)
            errors.Add("Location points count must be non-negative");

        // Speed validation
        if (AverageSpeedKmh < 0 || AverageSpeedKmh > 200)
            errors.Add("Average speed must be between 0 and 200 km/h");

        // Time validation
        if (DrivingTimeMinutes < 0 || DrivingTimeMinutes > 1440) // 24 hours max
            errors.Add("Driving time must be between 0 and 1440 minutes");

        // Logic consistency checks
        if (TotalDistanceKm > 0 && MotorizedTrips == 0)
            errors.Add("Cannot have distance without trips");

        if (MotorizedTrips > 0 && TotalDistanceKm == 0)
            errors.Add("Cannot have trips without distance");

        if (AverageSpeedKmh > 0 && (TotalDistanceKm == 0 || DrivingTimeMinutes == 0))
            errors.Add("Cannot have average speed without distance and time");

        // Transport mode validation
        if (TransportModes.Any(mode => !mode.IsIncludedInAnalysis()))
            errors.Add("Transport modes list contains non-motorized modes");

        return errors;
    }

    /// <summary>
    /// Determines if this daily summary represents a significant driving day.
    /// Used for filtering out days with minimal or no driving activity.
    /// </summary>
    /// <param name="minimumDistanceKm">Minimum distance threshold for a "driving day".</param>
    /// <returns>True if this day has meaningful driving activity.</returns>
    public bool IsSignificantDrivingDay(decimal minimumDistanceKm = 1.0m)
    {
        return TotalDistanceKm >= minimumDistanceKm && MotorizedTrips > 0;
    }

    /// <summary>
    /// Checks if this daily summary would be compatible with a given EV range.
    /// </summary>
    /// <param name="evRangeKm">The EV's single-charge range in kilometers.</param>
    /// <returns>True if the longest trip is within the EV's range capability.</returns>
    public bool IsCompatibleWithEVRange(int evRangeKm)
    {
        if (evRangeKm <= 0)
            throw new ArgumentException("EV range must be positive", nameof(evRangeKm));

        return LongestTripKm <= evRangeKm;
    }

    /// <summary>
    /// Gets the efficiency rating for this driving day based on average speed.
    /// Higher speeds typically indicate highway driving with better EV efficiency.
    /// </summary>
    /// <returns>A rating from 1-5, where 5 is most efficient for EVs.</returns>
    public int GetEfficiencyRating()
    {
        return AverageSpeedKmh switch
        {
            < 20 => 2,  // City driving with lots of stops
            < 40 => 3,  // Mixed city/suburban
            < 70 => 5,  // Optimal highway speeds for EVs
            < 100 => 4, // Higher highway speeds
            _ => 2,     // Very high speeds (less efficient)
        };
    }

    /// <summary>
    /// Creates a summary string for debugging and logging purposes.
    /// </summary>
    /// <returns>A human-readable summary of this daily trip data.</returns>
    public override string ToString()
    {
        return $"{Date:yyyy-MM-dd}: {TotalDistanceKm:F1}km in {MotorizedTrips} trips " +
               $"(longest: {LongestTripKm:F1}km, avg speed: {AverageSpeedKmh:F1}km/h)";
    }

    /// <summary>
    /// Identifies individual trips from a sequence of location points.
    /// Groups consecutive points by transport mode and time gaps.
    /// </summary>
    /// <param name="points">Ordered location points for motorized transport.</param>
    /// <returns>A list of trip summaries with distance and duration.</returns>
    private static List<TripSegment> IdentifyTrips(List<LocationPoint> points)
    {
        var trips = new List<TripSegment>();
        if (!points.Any()) return trips;

        var currentTrip = new List<LocationPoint> { points[0] };
        var currentMode = points[0].ActivityType;

        for (int i = 1; i < points.Count; i++)
        {
            var point = points[i];
            var prevPoint = points[i - 1];
            var timeDiff = (point.Timestamp - prevPoint.Timestamp).TotalMinutes;

            // Start new trip if mode changes significantly or there's a long gap
            if (point.ActivityType != currentMode || timeDiff > 15)
            {
                if (currentTrip.Count > 1)
                {
                    trips.Add(CalculateTripSegment(currentTrip));
                }
                currentTrip = new List<LocationPoint> { point };
                currentMode = point.ActivityType;
            }
            else
            {
                currentTrip.Add(point);
            }
        }

        // Add final trip
        if (currentTrip.Count > 1)
        {
            trips.Add(CalculateTripSegment(currentTrip));
        }

        return trips;
    }

    /// <summary>
    /// Calculates trip metrics from a sequence of location points.
    /// </summary>
    /// <param name="tripPoints">Location points for a single trip.</param>
    /// <returns>Trip segment with calculated distance and duration.</returns>
    private static TripSegment CalculateTripSegment(List<LocationPoint> tripPoints)
    {
        decimal totalDistance = 0;
        var startTime = tripPoints.First().Timestamp;
        var endTime = tripPoints.Last().Timestamp;

        for (int i = 0; i < tripPoints.Count - 1; i++)
        {
            var distance = tripPoints[i].CalculateDistanceKm(tripPoints[i + 1]);
            totalDistance += (decimal)distance;
        }

        return new TripSegment
        {
            DistanceKm = totalDistance,
            DurationMinutes = (int)(endTime - startTime).TotalMinutes,
            TransportMode = tripPoints.First().ActivityType,
        };
    }

    /// <summary>
    /// Calculates data quality metrics for location points.
    /// </summary>
    /// <param name="points">Location points to analyze.</param>
    /// <returns>Data quality score between 0 and 1.</returns>
    private static double CalculateDataQuality(List<LocationPoint> points)
    {
        if (!points.Any()) return 0.0;

        var pointsWithAccuracy = points.Count(p => p.Accuracy.HasValue);
        var pointsWithHighConfidence = points.Count(p => p.ActivityConfidence >= 70);

        return (pointsWithAccuracy + pointsWithHighConfidence) / (2.0 * points.Count);
    }

    /// <summary>
    /// Represents a single trip segment within a daily summary.
    /// </summary>
    private class TripSegment
    {
        public decimal DistanceKm { get; set; }
        public int DurationMinutes { get; set; }
        public TransportMode TransportMode { get; set; }
    }
}