using EVRangeAnalyzer.Models;

namespace EVRangeAnalyzer.Services;

/// <summary>
/// Service for high-performance distance calculations using the Haversine formula.
/// Provides accurate distance measurements for GPS coordinates with optimized algorithms.
/// Supports batch processing and caching for improved performance with large datasets.
/// </summary>
public interface IDistanceCalculationService
{
    /// <summary>
    /// Calculates the distance between two GPS coordinates using the Haversine formula.
    /// </summary>
    /// <param name="lat1">Latitude of first point in degrees.</param>
    /// <param name="lon1">Longitude of first point in degrees.</param>
    /// <param name="lat2">Latitude of second point in degrees.</param>
    /// <param name="lon2">Longitude of second point in degrees.</param>
    /// <returns>Distance between the points in kilometers.</returns>
    double CalculateDistance(double lat1, double lon1, double lat2, double lon2);

    /// <summary>
    /// Calculates the distance between two location points.
    /// </summary>
    /// <param name="point1">First location point.</param>
    /// <param name="point2">Second location point.</param>
    /// <returns>Distance between the points in kilometers.</returns>
    double CalculateDistance(LocationPoint point1, LocationPoint point2);

    /// <summary>
    /// Calculates distances for a sequence of location points and updates their distance-to-next values.
    /// </summary>
    /// <param name="locationPoints">Ordered sequence of location points.</param>
    /// <returns>The same location points with updated distance calculations.</returns>
    Task<List<LocationPoint>> CalculateDistancesAsync(IEnumerable<LocationPoint> locationPoints);

    /// <summary>
    /// Calculates the total distance for a trip composed of multiple location points.
    /// </summary>
    /// <param name="locationPoints">Ordered location points representing a trip.</param>
    /// <returns>Total trip distance in kilometers.</returns>
    double CalculateTripDistance(IEnumerable<LocationPoint> locationPoints);

    /// <summary>
    /// Calculates average speed between two location points based on distance and time.
    /// </summary>
    /// <param name="point1">Starting location point.</param>
    /// <param name="point2">Ending location point.</param>
    /// <returns>Average speed in km/h, or null if time difference is zero.</returns>
    double? CalculateAverageSpeed(LocationPoint point1, LocationPoint point2);

    /// <summary>
    /// Finds location points within a specified radius of a center point.
    /// </summary>
    /// <param name="centerPoint">Center point for radius search.</param>
    /// <param name="searchPoints">Collection of points to search within.</param>
    /// <param name="radiusKm">Search radius in kilometers.</param>
    /// <returns>Location points within the specified radius.</returns>
    Task<List<LocationPoint>> FindPointsWithinRadiusAsync(
        LocationPoint centerPoint,
        IEnumerable<LocationPoint> searchPoints,
        double radiusKm);

    /// <summary>
    /// Validates GPS coordinates for reasonable earth-bound values.
    /// </summary>
    /// <param name="latitude">Latitude to validate.</param>
    /// <param name="longitude">Longitude to validate.</param>
    /// <returns>True if coordinates are valid.</returns>
    bool AreCoordinatesValid(double latitude, double longitude);

    /// <summary>
    /// Calculates bearing (direction) between two GPS coordinates.
    /// </summary>
    /// <param name="lat1">Starting latitude in degrees.</param>
    /// <param name="lon1">Starting longitude in degrees.</param>
    /// <param name="lat2">Ending latitude in degrees.</param>
    /// <param name="lon2">Ending longitude in degrees.</param>
    /// <returns>Bearing in degrees (0-360), where 0 is North.</returns>
    double CalculateBearing(double lat1, double lon1, double lat2, double lon2);

    /// <summary>
    /// Gets distance calculation statistics for performance monitoring.
    /// </summary>
    /// <returns>Statistics about distance calculations performed.</returns>
    DistanceCalculationStats GetStatistics();
}

/// <summary>
/// High-performance implementation of distance calculations using optimized Haversine formula.
/// Includes caching and batch processing optimizations for large datasets.
/// </summary>
public class DistanceCalculationService : IDistanceCalculationService
{
    private readonly ILogger<DistanceCalculationService> _logger;
    
    /// <summary>
    /// Earth's radius in kilometers (mean radius).
    /// </summary>
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Conversion factor from degrees to radians.
    /// </summary>
    private const double DegreesToRadians = Math.PI / 180.0;

    /// <summary>
    /// Maximum reasonable distance between consecutive GPS points (km).
    /// Used for anomaly detection.
    /// </summary>
    private const double MaxReasonableDistance = 500.0;

    /// <summary>
    /// Statistics tracking for performance monitoring.
    /// </summary>
    private readonly DistanceCalculationStats _stats = new();

    public DistanceCalculationService(ILogger<DistanceCalculationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Validate coordinates
        if (!AreCoordinatesValid(lat1, lon1) || !AreCoordinatesValid(lat2, lon2))
        {
            _logger.LogWarning("Invalid coordinates provided: ({Lat1}, {Lon1}) to ({Lat2}, {Lon2})", 
                lat1, lon1, lat2, lon2);
            return 0.0;
        }

        // Quick check for identical points
        if (Math.Abs(lat1 - lat2) < 1e-9 && Math.Abs(lon1 - lon2) < 1e-9)
        {
            _stats.IncrementCalculations();
            return 0.0;
        }

        try
        {
            var distance = CalculateHaversineDistance(lat1, lon1, lat2, lon2);
            
            // Validate reasonable distance
            if (distance > MaxReasonableDistance)
            {
                _logger.LogWarning("Calculated distance {Distance:F2}km exceeds reasonable limit between " +
                                 "({Lat1:F6}, {Lon1:F6}) and ({Lat2:F6}, {Lon2:F6})", 
                    distance, lat1, lon1, lat2, lon2);
                _stats.IncrementAnomalies();
            }

            _stats.IncrementCalculations();
            _stats.UpdateMaxDistance(distance);
            
            return distance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate distance between ({Lat1}, {Lon1}) and ({Lat2}, {Lon2})", 
                lat1, lon1, lat2, lon2);
            _stats.IncrementErrors();
            return 0.0;
        }
    }

    /// <inheritdoc />
    public double CalculateDistance(LocationPoint point1, LocationPoint point2)
    {
        if (point1 == null) throw new ArgumentNullException(nameof(point1));
        if (point2 == null) throw new ArgumentNullException(nameof(point2));

        return CalculateDistance(point1.Latitude, point1.Longitude, point2.Latitude, point2.Longitude);
    }

    /// <inheritdoc />
    public async Task<List<LocationPoint>> CalculateDistancesAsync(IEnumerable<LocationPoint> locationPoints)
    {
        var points = locationPoints?.OrderBy(p => p.Timestamp).ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        
        if (points.Count < 2)
        {
            _logger.LogDebug("Insufficient points for distance calculation: {Count}", points.Count);
            return points;
        }

        var processedPoints = new List<LocationPoint>(points.Count);
        var batchStartTime = DateTime.UtcNow;

        for (int i = 0; i < points.Count; i++)
        {
            var currentPoint = points[i];
            
            if (i < points.Count - 1)
            {
                var nextPoint = points[i + 1];
                
                try
                {
                    // Calculate distance to next point
                    var distance = CalculateDistance(currentPoint, nextPoint);
                    currentPoint.DistanceToNextKm = (decimal)distance;

                    // Calculate time to next point
                    var timeDiff = nextPoint.Timestamp - currentPoint.Timestamp;
                    currentPoint.TimeToNextSeconds = timeDiff.TotalSeconds;

                    // Calculate velocity if time is positive
                    if (timeDiff.TotalHours > 0)
                    {
                        currentPoint.Velocity = distance / timeDiff.TotalHours;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to calculate distance/velocity for point at {Timestamp}: {Error}", 
                        currentPoint.Timestamp, ex.Message);
                    
                    // Set safe defaults
                    currentPoint.DistanceToNextKm = null;
                    currentPoint.TimeToNextSeconds = null;
                    currentPoint.Velocity = null;
                }
            }

            processedPoints.Add(currentPoint);
        }

        var processingTime = (DateTime.UtcNow - batchStartTime).TotalMilliseconds;
        _stats.UpdateBatchProcessingTime(processingTime);

        _logger.LogDebug("Processed {PointCount} points for distance calculation in {ProcessingTime:F0}ms", 
            points.Count, processingTime);

        return processedPoints;
    }

    /// <inheritdoc />
    public double CalculateTripDistance(IEnumerable<LocationPoint> locationPoints)
    {
        var points = locationPoints?.OrderBy(p => p.Timestamp).ToList() ?? throw new ArgumentNullException(nameof(locationPoints));
        
        if (points.Count < 2)
        {
            return 0.0;
        }

        var totalDistance = 0.0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var distance = CalculateDistance(points[i], points[i + 1]);
            totalDistance += distance;
        }

        _logger.LogDebug("Calculated total trip distance: {Distance:F2}km for {PointCount} points", 
            totalDistance, points.Count);

        return totalDistance;
    }

    /// <inheritdoc />
    public double? CalculateAverageSpeed(LocationPoint point1, LocationPoint point2)
    {
        if (point1 == null) throw new ArgumentNullException(nameof(point1));
        if (point2 == null) throw new ArgumentNullException(nameof(point2));

        var distance = CalculateDistance(point1, point2);
        var timeDiff = (point2.Timestamp - point1.Timestamp).TotalHours;

        if (timeDiff <= 0)
        {
            _logger.LogDebug("Zero or negative time difference between points, cannot calculate speed");
            return null;
        }

        var speed = distance / timeDiff;
        
        _logger.LogDebug("Calculated average speed: {Speed:F1}km/h over {Distance:F2}km in {Time:F1}h", 
            speed, distance, timeDiff);

        return speed;
    }

    /// <inheritdoc />
    public async Task<List<LocationPoint>> FindPointsWithinRadiusAsync(
        LocationPoint centerPoint,
        IEnumerable<LocationPoint> searchPoints,
        double radiusKm)
    {
        if (centerPoint == null) throw new ArgumentNullException(nameof(centerPoint));
        if (searchPoints == null) throw new ArgumentNullException(nameof(searchPoints));

        var withinRadius = new List<LocationPoint>();
        var searchList = searchPoints.ToList();

        await Task.Run(() =>
        {
            Parallel.ForEach(searchList, point =>
            {
                var distance = CalculateDistance(centerPoint, point);
                if (distance <= radiusKm)
                {
                    lock (withinRadius)
                    {
                        withinRadius.Add(point);
                    }
                }
            });
        });

        _logger.LogDebug("Found {Count} points within {Radius:F1}km of center point", 
            withinRadius.Count, radiusKm);

        return withinRadius.OrderBy(p => CalculateDistance(centerPoint, p)).ToList();
    }

    /// <inheritdoc />
    public bool AreCoordinatesValid(double latitude, double longitude)
    {
        return latitude >= -90.0 && latitude <= 90.0 && 
               longitude >= -180.0 && longitude <= 180.0 &&
               !double.IsNaN(latitude) && !double.IsNaN(longitude) &&
               !double.IsInfinity(latitude) && !double.IsInfinity(longitude);
    }

    /// <inheritdoc />
    public double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        if (!AreCoordinatesValid(lat1, lon1) || !AreCoordinatesValid(lat2, lon2))
        {
            return 0.0;
        }

        // Convert to radians
        var lat1Rad = lat1 * DegreesToRadians;
        var lat2Rad = lat2 * DegreesToRadians;
        var deltaLonRad = (lon2 - lon1) * DegreesToRadians;

        // Calculate bearing
        var y = Math.Sin(deltaLonRad) * Math.Cos(lat2Rad);
        var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) - 
                Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(deltaLonRad);

        var bearingRad = Math.Atan2(y, x);
        var bearingDeg = bearingRad / DegreesToRadians;

        // Normalize to 0-360 degrees
        return (bearingDeg + 360.0) % 360.0;
    }

    /// <inheritdoc />
    public DistanceCalculationStats GetStatistics()
    {
        return new DistanceCalculationStats(_stats);
    }

    /// <summary>
    /// Calculates distance using the Haversine formula with optimized trigonometric operations.
    /// </summary>
    private static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Convert degrees to radians
        var lat1Rad = lat1 * DegreesToRadians;
        var lon1Rad = lon1 * DegreesToRadians;
        var lat2Rad = lat2 * DegreesToRadians;
        var lon2Rad = lon2 * DegreesToRadians;

        // Calculate differences
        var deltaLat = lat2Rad - lat1Rad;
        var deltaLon = lon2Rad - lon1Rad;

        // Haversine formula
        var sinDeltaLatHalf = Math.Sin(deltaLat / 2);
        var sinDeltaLonHalf = Math.Sin(deltaLon / 2);
        
        var a = sinDeltaLatHalf * sinDeltaLatHalf +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                sinDeltaLonHalf * sinDeltaLonHalf;

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }
}

/// <summary>
/// Statistics for distance calculation operations.
/// </summary>
public class DistanceCalculationStats
{
    private long _totalCalculations;
    private long _totalErrors;
    private long _totalAnomalies;
    private double _maxDistanceCalculated;
    private double _totalBatchProcessingTime;
    private int _batchCount;

    public DistanceCalculationStats() { }

    public DistanceCalculationStats(DistanceCalculationStats source)
    {
        _totalCalculations = source._totalCalculations;
        _totalErrors = source._totalErrors;
        _totalAnomalies = source._totalAnomalies;
        _maxDistanceCalculated = source._maxDistanceCalculated;
        _totalBatchProcessingTime = source._totalBatchProcessingTime;
        _batchCount = source._batchCount;
    }

    public long TotalCalculations => _totalCalculations;
    public long TotalErrors => _totalErrors;
    public long TotalAnomalies => _totalAnomalies;
    public double MaxDistanceCalculated => _maxDistanceCalculated;
    public double AverageBatchProcessingTime => _batchCount > 0 ? _totalBatchProcessingTime / _batchCount : 0;
    public double ErrorRate => _totalCalculations > 0 ? (double)_totalErrors / _totalCalculations * 100 : 0;

    public void IncrementCalculations()
    {
        Interlocked.Increment(ref _totalCalculations);
    }

    public void IncrementErrors()
    {
        Interlocked.Increment(ref _totalErrors);
    }

    public void IncrementAnomalies()
    {
        Interlocked.Increment(ref _totalAnomalies);
    }

    public void UpdateMaxDistance(double distance)
    {
        var currentMax = _maxDistanceCalculated;
        while (distance > currentMax)
        {
            var original = Interlocked.CompareExchange(ref _maxDistanceCalculated, distance, currentMax);
            if (Math.Abs(original - currentMax) < 1e-9) break;
            currentMax = _maxDistanceCalculated;
        }
    }

    public void UpdateBatchProcessingTime(double timeMs)
    {
        _totalBatchProcessingTime += timeMs;
        Interlocked.Increment(ref _batchCount);
    }

    public override string ToString()
    {
        return $"Distance Calculations: {TotalCalculations:N0}, Errors: {TotalErrors:N0} ({ErrorRate:F1}%), " +
               $"Max Distance: {MaxDistanceCalculated:F1}km, Avg Batch Time: {AverageBatchProcessingTime:F1}ms";
    }
}