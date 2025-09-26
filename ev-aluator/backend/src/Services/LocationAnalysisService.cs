using EVRangeAnalyzer.Models;

namespace EVRangeAnalyzer.Services;

/// <summary>
/// Service for analyzing and filtering location points based on transport modes and data quality.
/// Handles trip segmentation, transport mode validation, and preparation for EV compatibility analysis.
/// </summary>
public interface ILocationAnalysisService
{
    /// <summary>
    /// Filters and analyzes location points for EV compatibility analysis.
    /// </summary>
    /// <param name="locationPoints">Raw location points from file processing.</param>
    /// <param name="cancellationToken">Cancellation token for operation cancellation.</param>
    /// <returns>Filtered and analyzed location points ready for trip calculations.</returns>
    Task<List<LocationPoint>> FilterAndAnalyzePointsAsync(
        IEnumerable<LocationPoint> locationPoints,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Segments location points into individual trips based on time gaps and transport mode changes.
    /// </summary>
    /// <param name="locationPoints">Ordered location points to segment.</param>
    /// <returns>List of trip segments with start/end markers.</returns>
    Task<List<TripSegment>> SegmentIntoTripsAsync(IEnumerable<LocationPoint> locationPoints);

    /// <summary>
    /// Validates and enriches transport mode classifications using speed analysis.
    /// </summary>
    /// <param name="locationPoints">Location points to validate.</param>
    /// <returns>Location points with validated and enriched transport modes.</returns>
    Task<List<LocationPoint>> ValidateTransportModesAsync(IEnumerable<LocationPoint> locationPoints);

    /// <summary>
    /// Calculates distances and velocities between consecutive location points.
    /// </summary>
    /// <param name="locationPoints">Ordered location points for calculation.</param>
    /// <returns>Location points with updated distance and velocity data.</returns>
    Task<List<LocationPoint>> CalculateDistancesAndVelocitiesAsync(IEnumerable<LocationPoint> locationPoints);

    /// <summary>
    /// Filters location points based on data quality criteria.
    /// </summary>
    /// <param name="locationPoints">Location points to filter.</param>
    /// <param name="minAccuracy">Minimum GPS accuracy threshold in meters.</param>
    /// <param name="minConfidence">Minimum activity confidence threshold (0-100).</param>
    /// <returns>High-quality location points suitable for analysis.</returns>
    Task<List<LocationPoint>> FilterByDataQualityAsync(
        IEnumerable<LocationPoint> locationPoints,
        int minAccuracy = 100,
        int minConfidence = 50);

    /// <summary>
    /// Identifies stationary periods and removes static location points.
    /// </summary>
    /// <param name="locationPoints">Location points to analyze.</param>
    /// <param name="maxStationarySpeed">Maximum speed to consider stationary (km/h).</param>
    /// <param name="minStationaryDuration">Minimum duration to consider stationary (minutes).</param>
    /// <returns>Location points with stationary periods marked or removed.</returns>
    Task<List<LocationPoint>> RemoveStationaryPeriodsAsync(
        IEnumerable<LocationPoint> locationPoints,
        double maxStationarySpeed = 5.0,
        int minStationaryDuration = 15);
}

/// <summary>
/// Implementation of location analysis service with transport mode filtering and trip segmentation.
/// </summary>
public class LocationAnalysisService : ILocationAnalysisService
{
    private readonly IDistanceCalculationService _distanceCalculationService;
    private readonly ILogger<LocationAnalysisService> _logger;

    /// <summary>
    /// Maximum time gap between points to consider them part of the same trip (minutes).
    /// </summary>
    private const int MaxTripGapMinutes = 30;

    /// <summary>
    /// Minimum trip duration to include in analysis (minutes).
    /// </summary>
    private const int MinTripDurationMinutes = 2;

    /// <summary>
    /// Maximum reasonable speed for ground transportation (km/h).
    /// </summary>
    private const double MaxReasonableSpeed = 200.0;

    public LocationAnalysisService(
        IDistanceCalculationService distanceCalculationService,
        ILogger<LocationAnalysisService> logger)
    {
        _distanceCalculationService = distanceCalculationService ?? throw new ArgumentNullException(nameof(distanceCalculationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<LocationPoint>> FilterAndAnalyzePointsAsync(
        IEnumerable<LocationPoint> locationPoints,
        CancellationToken cancellationToken = default)
    {
        var points = locationPoints?.OrderBy(p => p.Timestamp).ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        
        if (!points.Any())
        {
            _logger.LogDebug("No location points provided for analysis");
            return new List<LocationPoint>();
        }

        _logger.LogInformation("Starting analysis of {PointCount} location points", points.Count);

        try
        {
            // Step 1: Filter by data quality
            var qualityFiltered = await FilterByDataQualityAsync(points, cancellationToken: cancellationToken);
            _logger.LogDebug("After quality filtering: {PointCount} points", qualityFiltered.Count);

            // Step 2: Calculate distances and velocities
            var withDistances = await CalculateDistancesAndVelocitiesAsync(qualityFiltered);
            _logger.LogDebug("After distance calculations: {PointCount} points", withDistances.Count);

            // Step 3: Validate and enrich transport modes
            var withValidatedModes = await ValidateTransportModesAsync(withDistances);
            _logger.LogDebug("After transport mode validation: {PointCount} points", withValidatedModes.Count);

            // Step 4: Remove stationary periods
            var withoutStationary = await RemoveStationaryPeriodsAsync(withValidatedModes);
            _logger.LogDebug("After removing stationary periods: {PointCount} points", withoutStationary.Count);

            // Step 5: Filter for motorized transport only
            var motorizedOnly = withoutStationary.Where(p => p.ShouldIncludeInAnalysis()).ToList();
            _logger.LogDebug("After filtering to motorized transport: {PointCount} points", motorizedOnly.Count);

            _logger.LogInformation("Completed analysis: {FinalCount} points ready for EV compatibility analysis", motorizedOnly.Count);

            return motorizedOnly;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to filter and analyze location points");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<TripSegment>> SegmentIntoTripsAsync(IEnumerable<LocationPoint> locationPoints)
    {
        var points = locationPoints?.OrderBy(p => p.Timestamp).ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        var trips = new List<TripSegment>();

        if (points.Count < 2)
        {
            return trips;
        }

        var currentTrip = new List<LocationPoint> { points[0] };
        var currentMode = points[0].ActivityType;

        for (int i = 1; i < points.Count; i++)
        {
            var currentPoint = points[i];
            var previousPoint = points[i - 1];
            
            var timeDiff = (currentPoint.Timestamp - previousPoint.Timestamp).TotalMinutes;
            var modeChanged = currentPoint.ActivityType != currentMode;
            var significantModeChange = modeChanged && 
                                      (currentMode.IsIncludedInAnalysis() != currentPoint.ActivityType.IsIncludedInAnalysis());

            // Start new trip if:
            // 1. Time gap is too large
            // 2. Transport mode changes significantly
            if (timeDiff > MaxTripGapMinutes || significantModeChange)
            {
                if (IsValidTrip(currentTrip))
                {
                    trips.Add(await CreateTripSegmentAsync(currentTrip));
                }

                currentTrip = new List<LocationPoint> { currentPoint };
                currentMode = currentPoint.ActivityType;
            }
            else
            {
                currentTrip.Add(currentPoint);
            }
        }

        // Add the last trip
        if (IsValidTrip(currentTrip))
        {
            trips.Add(await CreateTripSegmentAsync(currentTrip));
        }

        _logger.LogDebug("Segmented {PointCount} points into {TripCount} trips", points.Count, trips.Count);

        return trips;
    }

    /// <inheritdoc />
    public async Task<List<LocationPoint>> ValidateTransportModesAsync(IEnumerable<LocationPoint> locationPoints)
    {
        var points = locationPoints?.ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        var validatedPoints = new List<LocationPoint>();

        foreach (var point in points)
        {
            try
            {
                var validatedPoint = ValidateTransportMode(point);
                validatedPoints.Add(validatedPoint);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to validate transport mode for point at {Timestamp}: {Error}", 
                    point.Timestamp, ex.Message);
                // Keep original point if validation fails
                validatedPoints.Add(point);
            }
        }

        return validatedPoints;
    }

    /// <inheritdoc />
    public async Task<List<LocationPoint>> CalculateDistancesAndVelocitiesAsync(IEnumerable<LocationPoint> locationPoints)
    {
        var points = locationPoints?.OrderBy(p => p.Timestamp).ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        
        if (points.Count < 2)
        {
            return points;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            var currentPoint = points[i];
            var nextPoint = points[i + 1];

            try
            {
                // Update distance and time to next point
                currentPoint.UpdateDistanceToNext(nextPoint);

                // Validate calculated velocity
                if (currentPoint.Velocity.HasValue && currentPoint.Velocity > MaxReasonableSpeed)
                {
                    _logger.LogDebug("Calculated velocity {Velocity} km/h exceeds reasonable limit, capping at {MaxSpeed} km/h",
                        currentPoint.Velocity, MaxReasonableSpeed);
                    currentPoint.Velocity = MaxReasonableSpeed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to calculate distance/velocity between points: {Error}", ex.Message);
                // Continue with next point pair
            }
        }

        return points;
    }

    /// <inheritdoc />
    public async Task<List<LocationPoint>> FilterByDataQualityAsync(
        IEnumerable<LocationPoint> locationPoints,
        int minAccuracy = 100,
        int minConfidence = 50)
    {
        var points = locationPoints?.ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        var filteredPoints = new List<LocationPoint>();

        foreach (var point in points)
        {
            // Check GPS accuracy
            if (point.Accuracy.HasValue && point.Accuracy > minAccuracy)
            {
                _logger.LogDebug("Excluding point with poor GPS accuracy: {Accuracy}m > {MinAccuracy}m",
                    point.Accuracy, minAccuracy);
                continue;
            }

            // Check activity confidence (only if available)
            if (point.ActivityConfidence.HasValue && point.ActivityConfidence < minConfidence)
            {
                _logger.LogDebug("Excluding point with low activity confidence: {Confidence}% < {MinConfidence}%",
                    point.ActivityConfidence, minConfidence);
                continue;
            }

            // Check coordinate validity
            if (Math.Abs(point.Latitude) < 0.01 && Math.Abs(point.Longitude) < 0.01)
            {
                _logger.LogDebug("Excluding point with invalid coordinates: ({Lat}, {Lon})",
                    point.Latitude, point.Longitude);
                continue;
            }

            filteredPoints.Add(point);
        }

        _logger.LogDebug("Quality filtering: {Original} → {Filtered} points (removed {Removed})",
            points.Count, filteredPoints.Count, points.Count - filteredPoints.Count);

        return filteredPoints;
    }

    /// <inheritdoc />
    public async Task<List<LocationPoint>> RemoveStationaryPeriodsAsync(
        IEnumerable<LocationPoint> locationPoints,
        double maxStationarySpeed = 5.0,
        int minStationaryDuration = 15)
    {
        var points = locationPoints?.OrderBy(p => p.Timestamp).ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        var nonStationaryPoints = new List<LocationPoint>();

        if (points.Count < 3)
        {
            return points;
        }

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            
            // Check if this point is part of a stationary period
            if (IsPartOfStationaryPeriod(points, i, maxStationarySpeed, minStationaryDuration))
            {
                _logger.LogDebug("Excluding stationary point at {Timestamp} (speed: {Speed:F1} km/h)",
                    point.Timestamp, point.Velocity ?? 0);
                continue;
            }

            nonStationaryPoints.Add(point);
        }

        _logger.LogDebug("Stationary period removal: {Original} → {Filtered} points (removed {Removed})",
            points.Count, nonStationaryPoints.Count, points.Count - nonStationaryPoints.Count);

        return nonStationaryPoints;
    }

    /// <summary>
    /// Validates and potentially corrects the transport mode for a location point based on speed analysis.
    /// </summary>
    private LocationPoint ValidateTransportMode(LocationPoint point)
    {
        // If no velocity data, return point as-is
        if (!point.Velocity.HasValue)
        {
            return point;
        }

        var speed = point.Velocity.Value;
        var originalMode = point.ActivityType;

        // Use speed-based validation to check if reported mode is reasonable
        if (!originalMode.IsSpeedValid(speed))
        {
            // Attempt to correct based on speed
            var correctedMode = InferTransportModeFromSpeed(speed);
            
            if (correctedMode != originalMode)
            {
                _logger.LogDebug("Transport mode correction: {Original} → {Corrected} based on speed {Speed:F1} km/h",
                    originalMode.GetDisplayName(), correctedMode.GetDisplayName(), speed);
                
                // Create a new point with corrected mode (preserve original confidence if available)
                var correctedPoint = new LocationPoint
                {
                    SessionId = point.SessionId,
                    Timestamp = point.Timestamp,
                    Latitude = point.Latitude,
                    Longitude = point.Longitude,
                    Accuracy = point.Accuracy,
                    ActivityType = correctedMode,
                    ActivityConfidence = point.ActivityConfidence ?? 50, // Default confidence for corrected modes
                    Velocity = point.Velocity,
                    Altitude = point.Altitude,
                    DistanceToNextKm = point.DistanceToNextKm,
                    TimeToNextSeconds = point.TimeToNextSeconds,
                };

                return correctedPoint;
            }
        }

        return point;
    }

    /// <summary>
    /// Infers transport mode from speed analysis.
    /// </summary>
    private TransportMode InferTransportModeFromSpeed(double speedKmh)
    {
        return speedKmh switch
        {
            < 8 => TransportMode.Walking,
            < 25 => TransportMode.OnBicycle,
            < 120 => TransportMode.InVehicle,
            _ => TransportMode.InFlight
        };
    }

    /// <summary>
    /// Checks if a trip is valid based on duration and distance criteria.
    /// </summary>
    private bool IsValidTrip(List<LocationPoint> tripPoints)
    {
        if (tripPoints.Count < 2)
            return false;

        var duration = (tripPoints.Last().Timestamp - tripPoints.First().Timestamp).TotalMinutes;
        return duration >= MinTripDurationMinutes;
    }

    /// <summary>
    /// Creates a trip segment from a list of location points.
    /// </summary>
    private async Task<TripSegment> CreateTripSegmentAsync(List<LocationPoint> tripPoints)
    {
        if (!tripPoints.Any())
            throw new ArgumentException("Trip points cannot be empty", nameof(tripPoints));

        // Mark trip boundaries
        tripPoints.First().IsTripStart = true;
        tripPoints.Last().IsTripEnd = true;

        // Calculate total distance
        var totalDistance = tripPoints
            .Where(p => p.DistanceToNextKm.HasValue)
            .Sum(p => p.DistanceToNextKm.Value);

        // Calculate trip duration
        var duration = tripPoints.Last().Timestamp - tripPoints.First().Timestamp;

        // Determine dominant transport mode
        var dominantMode = tripPoints
            .GroupBy(p => p.ActivityType)
            .OrderByDescending(g => g.Count())
            .First().Key;

        return new TripSegment
        {
            StartTime = tripPoints.First().Timestamp,
            EndTime = tripPoints.Last().Timestamp,
            Duration = duration,
            TotalDistanceKm = (decimal)totalDistance,
            LocationPoints = tripPoints,
            DominantTransportMode = dominantMode,
            PointCount = tripPoints.Count,
        };
    }

    /// <summary>
    /// Checks if a point is part of a stationary period.
    /// </summary>
    private bool IsPartOfStationaryPeriod(
        List<LocationPoint> points,
        int currentIndex,
        double maxStationarySpeed,
        int minStationaryDurationMinutes)
    {
        var currentPoint = points[currentIndex];

        // Check immediate velocity
        if (currentPoint.Velocity.HasValue && currentPoint.Velocity <= maxStationarySpeed)
        {
            // Look for extended stationary period
            var stationaryStart = currentIndex;
            var stationaryEnd = currentIndex;

            // Look backward for stationary period start
            while (stationaryStart > 0 && 
                   points[stationaryStart - 1].Velocity.HasValue &&
                   points[stationaryStart - 1].Velocity <= maxStationarySpeed)
            {
                stationaryStart--;
            }

            // Look forward for stationary period end
            while (stationaryEnd < points.Count - 1 && 
                   points[stationaryEnd + 1].Velocity.HasValue &&
                   points[stationaryEnd + 1].Velocity <= maxStationarySpeed)
            {
                stationaryEnd++;
            }

            // Check if stationary period is long enough
            var stationaryDuration = (points[stationaryEnd].Timestamp - points[stationaryStart].Timestamp).TotalMinutes;
            return stationaryDuration >= minStationaryDurationMinutes;
        }

        return false;
    }
}

/// <summary>
/// Represents a segmented trip with analysis metadata.
/// </summary>
public class TripSegment
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public decimal TotalDistanceKm { get; set; }
    public TransportMode DominantTransportMode { get; set; }
    public List<LocationPoint> LocationPoints { get; set; } = new();
    public int PointCount { get; set; }

    public double AverageSpeedKmh => Duration.TotalHours > 0 ? (double)(TotalDistanceKm / (decimal)Duration.TotalHours) : 0;
}