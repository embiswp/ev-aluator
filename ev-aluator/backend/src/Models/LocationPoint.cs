using System.ComponentModel.DataAnnotations;

namespace EVRangeAnalyzer.Models;

/// <summary>
/// Represents an individual GPS coordinate with timestamp and activity data from Google location history.
/// Created during JSON parsing and used for trip calculations and distance analysis.
/// Stored temporarily in memory during processing (not persisted to database).
/// </summary>
public class LocationPoint
{
    /// <summary>
    /// Gets or sets the unique identifier for this location point.
    /// Auto-generated for memory management during processing.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the session ID that owns this location point.
    /// Foreign key reference to UserSession for data isolation.
    /// </summary>
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the GPS recording timestamp (UTC).
    /// Parsed from Google's timestampMs field (milliseconds since epoch).
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the GPS latitude coordinate.
    /// Must be between -90 and +90 degrees (WGS84 coordinate system).
    /// </summary>
    [Required]
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and +90 degrees")]
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the GPS longitude coordinate.
    /// Must be between -180 and +180 degrees (WGS84 coordinate system).
    /// </summary>
    [Required]
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and +180 degrees")]
    public double Longitude { get; set; }

    /// <summary>
    /// Gets or sets the GPS accuracy in meters.
    /// Optional field from Google data indicating position uncertainty.
    /// </summary>
    [Range(0, 10000, ErrorMessage = "Accuracy must be between 0 and 10,000 meters")]
    public int? Accuracy { get; set; }

    /// <summary>
    /// Gets or sets the detected transport mode for this location point.
    /// Mapped from Google's activity type using TransportModeExtensions.
    /// </summary>
    [Required]
    public TransportMode ActivityType { get; set; } = TransportMode.Unknown;

    /// <summary>
    /// Gets or sets Google's confidence score for the detected activity.
    /// Range from 0-100, where higher values indicate more certainty.
    /// </summary>
    [Range(0, 100, ErrorMessage = "Activity confidence must be between 0 and 100")]
    public int? ActivityConfidence { get; set; }

    /// <summary>
    /// Gets or sets the calculated or provided velocity in km/h.
    /// Calculated from distance between consecutive points or provided by Google.
    /// </summary>
    [Range(0, 1000, ErrorMessage = "Velocity must be between 0 and 1000 km/h")]
    public double? Velocity { get; set; }

    /// <summary>
    /// Gets or sets the altitude in meters above sea level.
    /// Optional field from Google location data.
    /// </summary>
    public int? Altitude { get; set; }

    /// <summary>
    /// Gets or sets additional activity types if multiple activities were detected.
    /// Google sometimes provides multiple activity possibilities with different confidence levels.
    /// </summary>
    public List<(TransportMode Mode, int Confidence)>? AlternativeActivities { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this point represents the start of a new trip.
    /// Calculated during trip segmentation based on time gaps and transport mode changes.
    /// </summary>
    public bool IsTripStart { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this point represents the end of a trip.
    /// Calculated during trip segmentation based on time gaps and transport mode changes.
    /// </summary>
    public bool IsTripEnd { get; set; }

    /// <summary>
    /// Gets or sets the calculated distance to the next point in kilometers.
    /// Used for trip distance calculations and speed validation.
    /// </summary>
    [Range(0, 1000, ErrorMessage = "Distance to next point must be between 0 and 1000 km")]
    public double? DistanceToNextKm { get; set; }

    /// <summary>
    /// Gets or sets the time duration to the next point in seconds.
    /// Used for speed calculations and trip segmentation.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Time to next point must be non-negative")]
    public double? TimeToNextSeconds { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocationPoint"/> class.
    /// </summary>
    public LocationPoint()
    {
        Id = DateTime.UtcNow.Ticks; // Simple ID generation for memory-only objects
    }

    /// <summary>
    /// Creates a new LocationPoint from Google Takeout JSON data.
    /// </summary>
    /// <param name="sessionId">The session ID that owns this location point.</param>
    /// <param name="timestampMs">Google timestamp in milliseconds since epoch.</param>
    /// <param name="latitudeE7">Google latitude in E7 format (degrees * 10^7).</param>
    /// <param name="longitudeE7">Google longitude in E7 format (degrees * 10^7).</param>
    /// <param name="accuracy">GPS accuracy in meters (optional).</param>
    /// <param name="activityType">Google activity type string (optional).</param>
    /// <param name="activityConfidence">Google activity confidence (0-100, optional).</param>
    /// <param name="altitude">Altitude in meters (optional).</param>
    /// <returns>A new LocationPoint with validated coordinates and activity data.</returns>
    public static LocationPoint FromGoogleData(
        string sessionId,
        long timestampMs,
        int latitudeE7,
        int longitudeE7,
        int? accuracy = null,
        string? activityType = null,
        int? activityConfidence = null,
        int? altitude = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        // Convert Google's E7 format to decimal degrees
        var latitude = latitudeE7 / 1e7;
        var longitude = longitudeE7 / 1e7;

        // Convert milliseconds since epoch to DateTime
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;

        // Map Google activity type to TransportMode
        var transportMode = TransportModeExtensions.FromGoogleActivityType(activityType);

        var point = new LocationPoint
        {
            SessionId = sessionId,
            Timestamp = timestamp,
            Latitude = latitude,
            Longitude = longitude,
            Accuracy = accuracy,
            ActivityType = transportMode,
            ActivityConfidence = activityConfidence,
            Altitude = altitude,
        };

        // Validate the created point
        var validationErrors = point.Validate();
        if (validationErrors.Any())
        {
            throw new ArgumentException($"Invalid location point data: {string.Join(", ", validationErrors)}");
        }

        return point;
    }

    /// <summary>
    /// Validates the current location point data for logical consistency.
    /// </summary>
    /// <returns>A list of validation error messages, or empty list if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Required field validation
        if (string.IsNullOrWhiteSpace(SessionId))
            errors.Add("Session ID is required");

        // Coordinate validation
        if (Latitude < -90 || Latitude > 90)
            errors.Add("Latitude must be between -90 and +90 degrees");

        if (Longitude < -180 || Longitude > 180)
            errors.Add("Longitude must be between -180 and +180 degrees");

        // Accuracy validation
        if (Accuracy.HasValue && (Accuracy < 0 || Accuracy > 10000))
            errors.Add("Accuracy must be between 0 and 10,000 meters");

        // Activity confidence validation
        if (ActivityConfidence.HasValue && (ActivityConfidence < 0 || ActivityConfidence > 100))
            errors.Add("Activity confidence must be between 0 and 100");

        // Velocity validation
        if (Velocity.HasValue && (Velocity < 0 || Velocity > 1000))
            errors.Add("Velocity must be between 0 and 1000 km/h");

        // Distance validation
        if (DistanceToNextKm.HasValue && (DistanceToNextKm < 0 || DistanceToNextKm > 1000))
            errors.Add("Distance to next point must be between 0 and 1000 km");

        // Time validation
        if (TimeToNextSeconds.HasValue && TimeToNextSeconds < 0)
            errors.Add("Time to next point must be non-negative");

        // Speed consistency check
        if (Velocity.HasValue && ActivityType != TransportMode.Unknown)
        {
            if (!ActivityType.IsSpeedValid(Velocity.Value))
            {
                var (minSpeed, maxSpeed) = ActivityType.GetTypicalSpeedRange();
                errors.Add($"Velocity {Velocity:F1} km/h is outside typical range for {ActivityType.GetDisplayName()} ({minSpeed}-{maxSpeed} km/h)");
            }
        }

        return errors;
    }

    /// <summary>
    /// Calculates the distance between this point and another location point using the Haversine formula.
    /// </summary>
    /// <param name="otherPoint">The other location point to calculate distance to.</param>
    /// <returns>The distance in kilometers between the two points.</returns>
    public double CalculateDistanceKm(LocationPoint otherPoint)
    {
        if (otherPoint == null)
            throw new ArgumentNullException(nameof(otherPoint));

        return CalculateHaversineDistance(Latitude, Longitude, otherPoint.Latitude, otherPoint.Longitude);
    }

    /// <summary>
    /// Calculates the time difference between this point and another location point.
    /// </summary>
    /// <param name="otherPoint">The other location point to calculate time difference to.</param>
    /// <returns>The time difference as a TimeSpan.</returns>
    public TimeSpan CalculateTimeDifference(LocationPoint otherPoint)
    {
        if (otherPoint == null)
            throw new ArgumentNullException(nameof(otherPoint));

        return otherPoint.Timestamp - Timestamp;
    }

    /// <summary>
    /// Calculates the average speed between this point and another location point.
    /// </summary>
    /// <param name="otherPoint">The other location point to calculate speed to.</param>
    /// <returns>The average speed in km/h, or null if time difference is zero.</returns>
    public double? CalculateAverageSpeedKmh(LocationPoint otherPoint)
    {
        if (otherPoint == null)
            throw new ArgumentNullException(nameof(otherPoint));

        var distanceKm = CalculateDistanceKm(otherPoint);
        var timeDifference = CalculateTimeDifference(otherPoint);

        if (timeDifference.TotalHours <= 0)
            return null;

        return distanceKm / timeDifference.TotalHours;
    }

    /// <summary>
    /// Updates the distance and time to the next point for trip calculations.
    /// </summary>
    /// <param name="nextPoint">The next location point in the sequence.</param>
    public void UpdateDistanceToNext(LocationPoint nextPoint)
    {
        if (nextPoint == null)
        {
            DistanceToNextKm = null;
            TimeToNextSeconds = null;
            return;
        }

        DistanceToNextKm = CalculateDistanceKm(nextPoint);
        TimeToNextSeconds = CalculateTimeDifference(nextPoint).TotalSeconds;

        // Update velocity based on calculated distance and time
        if (TimeToNextSeconds > 0)
        {
            Velocity = (DistanceToNextKm * 3600) / TimeToNextSeconds; // Convert to km/h
        }
    }

    /// <summary>
    /// Determines if this location point should be included in EV analysis.
    /// Based on transport mode and data quality criteria.
    /// </summary>
    /// <returns>True if this point represents motorized transport and should be included in analysis.</returns>
    public bool ShouldIncludeInAnalysis()
    {
        // Only include motorized transport modes
        if (!ActivityType.IsIncludedInAnalysis())
            return false;

        // Exclude points with very low activity confidence (likely misclassified)
        if (ActivityConfidence.HasValue && ActivityConfidence < 50)
            return false;

        // Exclude points with unreasonably high GPS accuracy uncertainty
        if (Accuracy.HasValue && Accuracy > 1000) // More than 1km uncertainty
            return false;

        return true;
    }

    /// <summary>
    /// Creates a summary string for debugging and logging purposes.
    /// </summary>
    /// <returns>A human-readable summary of this location point.</returns>
    public override string ToString()
    {
        var confidence = ActivityConfidence.HasValue ? $" ({ActivityConfidence}%)" : "";
        var accuracy = Accuracy.HasValue ? $" ±{Accuracy}m" : "";
        var velocity = Velocity.HasValue ? $" @{Velocity:F1}km/h" : "";
        
        return $"{Timestamp:yyyy-MM-dd HH:mm:ss} ({Latitude:F6}, {Longitude:F6}){accuracy} - {ActivityType.GetDisplayName()}{confidence}{velocity}";
    }

    /// <summary>
    /// Calculates the distance between two geographic points using the Haversine formula.
    /// This provides good accuracy for distances up to a few hundred kilometers.
    /// </summary>
    /// <param name="lat1">Latitude of first point in degrees.</param>
    /// <param name="lon1">Longitude of first point in degrees.</param>
    /// <param name="lat2">Latitude of second point in degrees.</param>
    /// <param name="lon2">Longitude of second point in degrees.</param>
    /// <returns>Distance between the points in kilometers.</returns>
    private static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        // Convert degrees to radians
        var lat1Rad = lat1 * Math.PI / 180.0;
        var lon1Rad = lon1 * Math.PI / 180.0;
        var lat2Rad = lat2 * Math.PI / 180.0;
        var lon2Rad = lon2 * Math.PI / 180.0;

        // Calculate differences
        var deltaLat = lat2Rad - lat1Rad;
        var deltaLon = lon2Rad - lon1Rad;

        // Haversine formula
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }
}