using EVRangeAnalyzer.Models;
using System.Text.Json;

namespace EVRangeAnalyzer.Services;

/// <summary>
/// Service for processing Google Takeout location history JSON files.
/// Handles streaming deserialization, validation, and parsing of large files (up to 100MB).
/// Supports both Records.json and Semantic Location History formats.
/// </summary>
public interface IFileProcessingService
{
    /// <summary>
    /// Processes a Google Takeout location history JSON file stream.
    /// </summary>
    /// <param name="sessionId">Session ID that owns this processing operation.</param>
    /// <param name="fileName">Original filename for metadata tracking.</param>
    /// <param name="fileStream">Stream containing the JSON data.</param>
    /// <param name="fileSizeMB">File size in megabytes.</param>
    /// <param name="cancellationToken">Cancellation token for operation cancellation.</param>
    /// <returns>Processing result with location points and metadata.</returns>
    Task<FileProcessingResult> ProcessLocationHistoryAsync(
        string sessionId,
        string fileName,
        Stream fileStream,
        decimal fileSizeMB,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates Google Takeout JSON file structure and format.
    /// </summary>
    /// <param name="fileStream">Stream containing the JSON data to validate.</param>
    /// <param name="maxSizeMB">Maximum allowed file size in megabytes.</param>
    /// <returns>Validation result with any errors found.</returns>
    Task<FileValidationResult> ValidateFileAsync(Stream fileStream, decimal maxSizeMB = 100);

    /// <summary>
    /// Estimates processing time based on file size and system performance.
    /// </summary>
    /// <param name="fileSizeMB">File size in megabytes.</param>
    /// <returns>Estimated processing time in milliseconds.</returns>
    int EstimateProcessingTime(decimal fileSizeMB);

    /// <summary>
    /// Gets supported Google Takeout file formats and their descriptions.
    /// </summary>
    /// <returns>Dictionary of supported formats and descriptions.</returns>
    Dictionary<string, string> GetSupportedFormats();
}

/// <summary>
/// Implementation of file processing service with streaming JSON deserialization.
/// </summary>
public class FileProcessingService : IFileProcessingService
{
    private readonly ILogger<FileProcessingService> _logger;
    private readonly ILocationAnalysisService _locationAnalysisService;

    /// <summary>
    /// Maximum file size in megabytes allowed for processing.
    /// </summary>
    private const decimal MaxFileSizeMB = 100;

    /// <summary>
    /// Buffer size for streaming operations (1MB).
    /// </summary>
    private const int StreamBufferSize = 1024 * 1024;

    /// <summary>
    /// Supported Google Takeout JSON file formats.
    /// </summary>
    private static readonly Dictionary<string, string> SupportedFormats = new()
    {
        { "Records.json", "Raw GPS coordinates with timestamps from Google Location History" },
        { "Semantic Location History.json", "Processed timeline data with activity recognition" },
        { "Location History.json", "Legacy format with combined GPS and activity data" },
    };

    public FileProcessingService(
        ILogger<FileProcessingService> logger,
        ILocationAnalysisService locationAnalysisService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _locationAnalysisService = locationAnalysisService ?? throw new ArgumentNullException(nameof(locationAnalysisService));
    }

    /// <inheritdoc />
    public async Task<FileProcessingResult> ProcessLocationHistoryAsync(
        string sessionId,
        string fileName,
        Stream fileStream,
        decimal fileSizeMB,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));

        var startTime = DateTime.UtcNow;
        var result = new FileProcessingResult
        {
            SessionId = sessionId,
            FileName = fileName,
            FileSizeMB = fileSizeMB,
            ProcessingStartedAt = startTime,
        };

        try
        {
            _logger.LogInformation("Starting processing of file {FileName} ({FileSizeMB:F1}MB) for session {SessionId}",
                fileName, fileSizeMB, sessionId);

            // Validate file before processing
            fileStream.Position = 0;
            var validation = await ValidateFileAsync(fileStream, MaxFileSizeMB);
            if (!validation.IsValid)
            {
                result.ProcessingError = string.Join("; ", validation.Errors);
                result.ProcessingCompletedAt = DateTime.UtcNow;
                return result;
            }

            // Reset stream position for processing
            fileStream.Position = 0;

            // Determine file format and process accordingly
            var locationPoints = new List<LocationPoint>();
            
            if (IsRecordsJsonFormat(fileName))
            {
                locationPoints = await ProcessRecordsJsonAsync(sessionId, fileStream, cancellationToken);
            }
            else if (IsSemanticLocationHistoryFormat(fileName))
            {
                locationPoints = await ProcessSemanticLocationHistoryAsync(sessionId, fileStream, cancellationToken);
            }
            else
            {
                locationPoints = await ProcessLegacyLocationHistoryAsync(sessionId, fileStream, cancellationToken);
            }

            // Filter and analyze location points
            var analyzedPoints = await _locationAnalysisService.FilterAndAnalyzePointsAsync(locationPoints, cancellationToken);

            result.LocationPoints = analyzedPoints;
            result.TotalLocationPoints = locationPoints.Count;
            result.ProcessedLocationPoints = analyzedPoints.Count;
            result.ProcessingCompletedAt = DateTime.UtcNow;
            result.ActualProcessingTimeMs = (int)(result.ProcessingCompletedAt.Value - startTime).TotalMilliseconds;

            // Calculate date range
            if (analyzedPoints.Any())
            {
                result.DateRangeStart = analyzedPoints.Min(p => p.Timestamp);
                result.DateRangeEnd = analyzedPoints.Max(p => p.Timestamp);
            }

            result.IsSuccessful = true;

            _logger.LogInformation("Successfully processed {TotalPoints} location points ({ProcessedPoints} after filtering) " +
                                 "from file {FileName} in {ProcessingTime}ms",
                result.TotalLocationPoints, result.ProcessedLocationPoints, fileName, result.ActualProcessingTimeMs);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing of file {FileName} was cancelled", fileName);
            result.ProcessingError = "Processing was cancelled";
            result.ProcessingCompletedAt = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file {FileName} for session {SessionId}", fileName, sessionId);
            result.ProcessingError = $"Processing failed: {ex.Message}";
            result.ProcessingCompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    /// <inheritdoc />
    public async Task<FileValidationResult> ValidateFileAsync(Stream fileStream, decimal maxSizeMB = 100)
    {
        var result = new FileValidationResult();

        try
        {
            // Check file size
            if (fileStream.Length > (long)(maxSizeMB * 1024 * 1024))
            {
                result.Errors.Add($"File size ({fileStream.Length / 1024 / 1024:F1}MB) exceeds maximum allowed size ({maxSizeMB}MB)");
            }

            // Check if stream is readable
            if (!fileStream.CanRead)
            {
                result.Errors.Add("File stream is not readable");
                return result;
            }

            // Validate JSON structure (read first 1KB to check format)
            fileStream.Position = 0;
            var buffer = new byte[Math.Min(1024, fileStream.Length)];
            await fileStream.ReadAsync(buffer, 0, buffer.Length);
            var jsonSample = System.Text.Encoding.UTF8.GetString(buffer);

            try
            {
                using var document = JsonDocument.Parse(jsonSample);
                // Basic validation that it's valid JSON
                result.DetectedFormat = DetectGoogleTakeoutFormat(jsonSample);
            }
            catch (JsonException ex)
            {
                result.Errors.Add($"Invalid JSON format: {ex.Message}");
            }

            // Reset stream position
            fileStream.Position = 0;

            result.IsValid = !result.Errors.Any();
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Validation failed: {ex.Message}");
            return result;
        }
    }

    /// <inheritdoc />
    public int EstimateProcessingTime(decimal fileSizeMB)
    {
        // Base processing time estimation: ~1000ms per MB + 2000ms overhead
        const int timePerMB = 1000; // milliseconds
        const int baseOverhead = 2000; // milliseconds
        
        return (int)(timePerMB * fileSizeMB + baseOverhead);
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetSupportedFormats()
    {
        return new Dictionary<string, string>(SupportedFormats);
    }

    /// <summary>
    /// Processes Google Records.json format (raw GPS coordinates).
    /// </summary>
    private async Task<List<LocationPoint>> ProcessRecordsJsonAsync(
        string sessionId,
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        var locationPoints = new List<LocationPoint>();
        
        using var reader = new StreamReader(fileStream, bufferSize: StreamBufferSize);
        var jsonString = await reader.ReadToEndAsync();
        
        using var document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;

        if (root.TryGetProperty("locations", out var locations))
        {
            foreach (var location in locations.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var point = ParseRecordLocation(sessionId, location);
                    if (point != null)
                    {
                        locationPoints.Add(point);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to parse location record: {Error}", ex.Message);
                    // Continue processing other points
                }
            }
        }

        return locationPoints;
    }

    /// <summary>
    /// Processes Semantic Location History format (timeline with activities).
    /// </summary>
    private async Task<List<LocationPoint>> ProcessSemanticLocationHistoryAsync(
        string sessionId,
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        var locationPoints = new List<LocationPoint>();
        
        using var reader = new StreamReader(fileStream, bufferSize: StreamBufferSize);
        var jsonString = await reader.ReadToEndAsync();
        
        using var document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;

        if (root.TryGetProperty("timelineObjects", out var timelineObjects))
        {
            foreach (var timelineObject in timelineObjects.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var points = ParseSemanticTimelineObject(sessionId, timelineObject);
                    locationPoints.AddRange(points);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to parse timeline object: {Error}", ex.Message);
                    // Continue processing other objects
                }
            }
        }

        return locationPoints;
    }

    /// <summary>
    /// Processes legacy Location History format.
    /// </summary>
    private async Task<List<LocationPoint>> ProcessLegacyLocationHistoryAsync(
        string sessionId,
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        // Legacy format is similar to Records.json
        return await ProcessRecordsJsonAsync(sessionId, fileStream, cancellationToken);
    }

    /// <summary>
    /// Parses a location record from Records.json format.
    /// </summary>
    private LocationPoint? ParseRecordLocation(string sessionId, JsonElement location)
    {
        if (!location.TryGetProperty("timestampMs", out var timestampElement) ||
            !location.TryGetProperty("latitudeE7", out var latElement) ||
            !location.TryGetProperty("longitudeE7", out var lonElement))
        {
            return null;
        }

        if (!long.TryParse(timestampElement.GetString(), out var timestampMs) ||
            !int.TryParse(latElement.GetString(), out var latitudeE7) ||
            !int.TryParse(lonElement.GetString(), out var longitudeE7))
        {
            return null;
        }

        // Extract optional fields
        int? accuracy = null;
        if (location.TryGetProperty("accuracy", out var accuracyElement))
        {
            accuracy = accuracyElement.GetInt32();
        }

        string? activityType = null;
        int? activityConfidence = null;
        
        if (location.TryGetProperty("activity", out var activities))
        {
            var activityArray = activities.EnumerateArray().FirstOrDefault();
            if (activityArray.ValueKind != JsonValueKind.Undefined)
            {
                if (activityArray.TryGetProperty("activities", out var activityList))
                {
                    var topActivity = activityList.EnumerateArray().FirstOrDefault();
                    if (topActivity.ValueKind != JsonValueKind.Undefined)
                    {
                        activityType = topActivity.GetProperty("type").GetString();
                        activityConfidence = topActivity.GetProperty("confidence").GetInt32();
                    }
                }
            }
        }

        int? altitude = null;
        if (location.TryGetProperty("altitude", out var altElement))
        {
            altitude = altElement.GetInt32();
        }

        return LocationPoint.FromGoogleData(
            sessionId,
            timestampMs,
            latitudeE7,
            longitudeE7,
            accuracy,
            activityType,
            activityConfidence,
            altitude);
    }

    /// <summary>
    /// Parses timeline objects from Semantic Location History format.
    /// </summary>
    private List<LocationPoint> ParseSemanticTimelineObject(string sessionId, JsonElement timelineObject)
    {
        var points = new List<LocationPoint>();

        // Handle activity segments with waypoints
        if (timelineObject.TryGetProperty("activitySegment", out var activitySegment))
        {
            points.AddRange(ParseActivitySegment(sessionId, activitySegment));
        }

        // Handle place visits
        if (timelineObject.TryGetProperty("placeVisit", out var placeVisit))
        {
            var point = ParsePlaceVisit(sessionId, placeVisit);
            if (point != null)
            {
                points.Add(point);
            }
        }

        return points;
    }

    /// <summary>
    /// Parses activity segments with waypoints.
    /// </summary>
    private List<LocationPoint> ParseActivitySegment(string sessionId, JsonElement activitySegment)
    {
        var points = new List<LocationPoint>();

        if (!activitySegment.TryGetProperty("activityType", out var activityTypeElement))
            return points;

        var activityType = activityTypeElement.GetString();
        var confidence = activitySegment.TryGetProperty("confidence", out var confElement) 
            ? (int?)confElement.GetInt32() : null;

        // Extract waypoints if available
        if (activitySegment.TryGetProperty("waypointPath", out var waypointPath) &&
            waypointPath.TryGetProperty("waypoints", out var waypoints))
        {
            foreach (var waypoint in waypoints.EnumerateArray())
            {
                try
                {
                    var point = ParseWaypoint(sessionId, waypoint, activityType, confidence);
                    if (point != null)
                    {
                        points.Add(point);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to parse waypoint: {Error}", ex.Message);
                }
            }
        }

        return points;
    }

    /// <summary>
    /// Parses a waypoint from activity segment.
    /// </summary>
    private LocationPoint? ParseWaypoint(string sessionId, JsonElement waypoint, string? activityType, int? confidence)
    {
        if (!waypoint.TryGetProperty("latE7", out var latElement) ||
            !waypoint.TryGetProperty("lngE7", out var lonElement))
        {
            return null;
        }

        var latitudeE7 = latElement.GetInt32();
        var longitudeE7 = lonElement.GetInt32();
        
        // Use current timestamp as approximation (semantic data may not have precise timestamps for waypoints)
        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return LocationPoint.FromGoogleData(
            sessionId,
            timestampMs,
            latitudeE7,
            longitudeE7,
            activityType: activityType,
            activityConfidence: confidence);
    }

    /// <summary>
    /// Parses place visits (stationary locations).
    /// </summary>
    private LocationPoint? ParsePlaceVisit(string sessionId, JsonElement placeVisit)
    {
        // Place visits are typically stationary, so we'll skip them for driving analysis
        return null;
    }

    /// <summary>
    /// Detects Google Takeout file format from JSON structure.
    /// </summary>
    private static string DetectGoogleTakeoutFormat(string jsonSample)
    {
        if (jsonSample.Contains("\"locations\""))
        {
            return "Records.json";
        }
        
        if (jsonSample.Contains("\"timelineObjects\""))
        {
            return "Semantic Location History";
        }
        
        return "Legacy Location History";
    }

    /// <summary>
    /// Checks if filename indicates Records.json format.
    /// </summary>
    private static bool IsRecordsJsonFormat(string fileName) => 
        fileName.Contains("Records", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if filename indicates Semantic Location History format.
    /// </summary>
    private static bool IsSemanticLocationHistoryFormat(string fileName) => 
        fileName.Contains("Semantic", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Result of file processing operation.
/// </summary>
public class FileProcessingResult
{
    public string SessionId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public decimal FileSizeMB { get; set; }
    public DateTime ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public int? ActualProcessingTimeMs { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ProcessingError { get; set; }
    public List<LocationPoint> LocationPoints { get; set; } = new();
    public int TotalLocationPoints { get; set; }
    public int ProcessedLocationPoints { get; set; }
    public DateTime? DateRangeStart { get; set; }
    public DateTime? DateRangeEnd { get; set; }
}

/// <summary>
/// Result of file validation operation.
/// </summary>
public class FileValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? DetectedFormat { get; set; }
}