using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EVRangeAnalyzer.Models;

/// <summary>
/// Represents parsed Google Takeout JSON containing raw location points.
/// Created during file upload and destroyed when the session ends.
/// Provides metadata about the uploaded file and processing progress.
/// </summary>
public class LocationHistoryData
{
    /// <summary>
    /// Gets or sets the session ID that owns this location history data.
    /// Foreign key reference to UserSession.
    /// </summary>
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original filename of the uploaded Google Takeout JSON file.
    /// Used for display purposes and processing logs.
    /// </summary>
    [Required]
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in megabytes for processing metrics and validation.
    /// Must be ≤ 100MB based on upload limits.
    /// </summary>
    [Required]
    [Range(0, 100, ErrorMessage = "File size must be between 0 and 100 MB")]
    public decimal FileSizeMB { get; set; }

    /// <summary>
    /// Gets or sets the total number of location points found in the JSON file.
    /// Includes all GPS coordinates regardless of parsing success.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Total location points must be non-negative")]
    public int TotalLocationPoints { get; set; }

    /// <summary>
    /// Gets or sets the number of location points successfully parsed and validated.
    /// Must be ≤ TotalLocationPoints.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Processed location points must be non-negative")]
    public int ProcessedLocationPoints { get; set; }

    /// <summary>
    /// Gets or sets the earliest timestamp found in the location data (UTC).
    /// Used to establish the date range of the dataset.
    /// </summary>
    public DateTime? DateRangeStart { get; set; }

    /// <summary>
    /// Gets or sets the latest timestamp found in the location data (UTC).
    /// Used to establish the date range of the dataset.
    /// </summary>
    public DateTime? DateRangeEnd { get; set; }

    /// <summary>
    /// Gets or sets the current processing status of the uploaded file.
    /// Tracks progress from upload through parsing to completion.
    /// </summary>
    [Required]
    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Uploading;

    /// <summary>
    /// Gets or sets the timestamp when the file was uploaded (UTC).
    /// Used for processing time calculations and cleanup.
    /// </summary>
    [Required]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when processing started (UTC).
    /// Null if processing hasn't started yet.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when processing completed (UTC).
    /// Null if processing is still in progress or failed.
    /// </summary>
    public DateTime? ProcessingCompletedAt { get; set; }

    /// <summary>
    /// Gets or sets error information if processing failed.
    /// Contains details about what went wrong during parsing or validation.
    /// </summary>
    public string? ProcessingError { get; set; }

    /// <summary>
    /// Gets or sets the estimated processing time in milliseconds.
    /// Calculated based on file size and system performance.
    /// </summary>
    public int? EstimatedProcessingTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the actual processing time in milliseconds.
    /// Measured from start to completion of parsing and validation.
    /// </summary>
    public int? ActualProcessingTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the uploaded file.
    /// Should be "application/json" for valid Google Takeout files.
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for this upload operation.
    /// Used to track progress and link with processing status updates.
    /// </summary>
    public string UploadId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Initializes a new instance of the <see cref="LocationHistoryData"/> class.
    /// </summary>
    public LocationHistoryData()
    {
        UploadedAt = DateTime.UtcNow;
        ProcessingStatus = ProcessingStatus.Uploading;
    }

    /// <summary>
    /// Creates a new LocationHistoryData instance from an uploaded file.
    /// </summary>
    /// <param name="sessionId">The session ID that owns this data.</param>
    /// <param name="fileName">The original filename.</param>
    /// <param name="fileSizeMB">The file size in megabytes.</param>
    /// <param name="mimeType">The MIME type of the uploaded file.</param>
    /// <returns>A new LocationHistoryData instance ready for processing.</returns>
    public static LocationHistoryData CreateFromUpload(string sessionId, string fileName, decimal fileSizeMB, string? mimeType = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

        if (fileSizeMB < 0 || fileSizeMB > 100)
            throw new ArgumentException("File size must be between 0 and 100 MB", nameof(fileSizeMB));

        if (!IsValidJsonFileName(fileName))
            throw new ArgumentException("File must have .json extension", nameof(fileName));

        return new LocationHistoryData
        {
            SessionId = sessionId,
            OriginalFileName = fileName,
            FileSizeMB = fileSizeMB,
            MimeType = mimeType ?? "application/json",
            EstimatedProcessingTimeMs = EstimateProcessingTime(fileSizeMB),
        };
    }

    /// <summary>
    /// Validates the current state of the location history data.
    /// Checks for logical consistency and required field validation.
    /// </summary>
    /// <returns>A list of validation error messages, or empty list if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Required field validation
        if (string.IsNullOrWhiteSpace(SessionId))
            errors.Add("Session ID is required");

        if (string.IsNullOrWhiteSpace(OriginalFileName))
            errors.Add("Original file name is required");

        // File validation
        if (FileSizeMB < 0 || FileSizeMB > 100)
            errors.Add("File size must be between 0 and 100 MB");

        if (!IsValidJsonFileName(OriginalFileName))
            errors.Add("File must have .json extension");

        // Processing count validation
        if (ProcessedLocationPoints < 0)
            errors.Add("Processed location points cannot be negative");

        if (TotalLocationPoints < 0)
            errors.Add("Total location points cannot be negative");

        if (ProcessedLocationPoints > TotalLocationPoints)
            errors.Add("Processed location points cannot exceed total location points");

        // Date range validation
        if (DateRangeStart.HasValue && DateRangeEnd.HasValue && DateRangeStart > DateRangeEnd)
            errors.Add("Date range start must be before or equal to date range end");

        // Processing timestamp validation
        if (ProcessingStartedAt.HasValue && ProcessingStartedAt < UploadedAt)
            errors.Add("Processing cannot start before upload time");

        if (ProcessingCompletedAt.HasValue && ProcessingStartedAt.HasValue && ProcessingCompletedAt < ProcessingStartedAt)
            errors.Add("Processing completion time cannot be before start time");

        return errors;
    }

    /// <summary>
    /// Starts the processing phase and updates relevant timestamps.
    /// </summary>
    public void StartProcessing()
    {
        if (ProcessingStatus == ProcessingStatus.Uploading)
        {
            ProcessingStatus = ProcessingStatus.Parsing;
            ProcessingStartedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Updates the processing status during different phases.
    /// </summary>
    /// <param name="status">The new processing status.</param>
    /// <param name="error">Optional error message if status is Failed.</param>
    public void UpdateProcessingStatus(ProcessingStatus status, string? error = null)
    {
        ProcessingStatus = status;

        if (status == ProcessingStatus.Processing && !ProcessingStartedAt.HasValue)
        {
            ProcessingStartedAt = DateTime.UtcNow;
        }

        if (status == ProcessingStatus.Completed || status == ProcessingStatus.Failed)
        {
            ProcessingCompletedAt = DateTime.UtcNow;

            if (ProcessingStartedAt.HasValue)
            {
                ActualProcessingTimeMs = (int)(ProcessingCompletedAt.Value - ProcessingStartedAt.Value).TotalMilliseconds;
            }

            if (status == ProcessingStatus.Failed && !string.IsNullOrWhiteSpace(error))
            {
                ProcessingError = error;
            }
        }
    }

    /// <summary>
    /// Updates location point counts during parsing.
    /// </summary>
    /// <param name="totalPoints">Total location points found in the JSON.</param>
    /// <param name="processedPoints">Successfully parsed location points.</param>
    public void UpdateLocationCounts(int totalPoints, int processedPoints)
    {
        if (totalPoints < 0)
            throw new ArgumentException("Total points cannot be negative", nameof(totalPoints));

        if (processedPoints < 0)
            throw new ArgumentException("Processed points cannot be negative", nameof(processedPoints));

        if (processedPoints > totalPoints)
            throw new ArgumentException("Processed points cannot exceed total points");

        TotalLocationPoints = totalPoints;
        ProcessedLocationPoints = processedPoints;
    }

    /// <summary>
    /// Updates the date range based on processed location points.
    /// </summary>
    /// <param name="startDate">The earliest timestamp in the data.</param>
    /// <param name="endDate">The latest timestamp in the data.</param>
    public void UpdateDateRange(DateTime startDate, DateTime endDate)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date must be before or equal to end date");

        DateRangeStart = startDate;
        DateRangeEnd = endDate;
    }

    /// <summary>
    /// Gets the date range span in days.
    /// </summary>
    /// <returns>The number of days covered by the location data, or null if range is not set.</returns>
    public int? GetDateRangeDays()
    {
        if (!DateRangeStart.HasValue || !DateRangeEnd.HasValue)
            return null;

        return (int)(DateRangeEnd.Value - DateRangeStart.Value).TotalDays + 1;
    }

    /// <summary>
    /// Gets the processing completion percentage (0-100).
    /// </summary>
    /// <returns>Processing progress as a percentage, or 0 if not started.</returns>
    public double GetProcessingProgress()
    {
        return ProcessingStatus switch
        {
            ProcessingStatus.Uploading => 10,
            ProcessingStatus.Parsing => 40,
            ProcessingStatus.Processing => 80,
            ProcessingStatus.Completed => 100,
            ProcessingStatus.Failed => 0,
            _ => 0,
        };
    }

    /// <summary>
    /// Determines if the file processing was successful.
    /// </summary>
    /// <returns>True if processing completed successfully with usable data.</returns>
    public bool IsProcessingSuccessful()
    {
        return ProcessingStatus == ProcessingStatus.Completed && ProcessedLocationPoints > 0;
    }

    /// <summary>
    /// Gets a summary of the processing results for display.
    /// </summary>
    /// <returns>A human-readable summary of the processing status.</returns>
    public string GetProcessingSummary()
    {
        return ProcessingStatus switch
        {
            ProcessingStatus.Uploading => "File upload in progress...",
            ProcessingStatus.Parsing => "Parsing JSON structure...",
            ProcessingStatus.Processing => "Processing location points...",
            ProcessingStatus.Completed => $"Successfully processed {ProcessedLocationPoints:N0} of {TotalLocationPoints:N0} location points",
            ProcessingStatus.Failed => $"Processing failed: {ProcessingError ?? "Unknown error"}",
            _ => "Unknown processing status",
        };
    }

    /// <summary>
    /// Validates that a filename has the .json extension (case-insensitive).
    /// </summary>
    /// <param name="fileName">The filename to validate.</param>
    /// <returns>True if the filename has a .json extension.</returns>
    private static bool IsValidJsonFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        return Path.GetExtension(fileName).Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Estimates processing time based on file size and system performance metrics.
    /// </summary>
    /// <param name="fileSizeMB">File size in megabytes.</param>
    /// <returns>Estimated processing time in milliseconds.</returns>
    private static int EstimateProcessingTime(decimal fileSizeMB)
    {
        // Base processing time: ~1 second per MB + overhead
        // This is a rough estimate and can be calibrated based on actual performance
        const int baseTimePerMB = 1000; // milliseconds
        const int baseOverhead = 2000; // milliseconds

        return (int)(baseTimePerMB * fileSizeMB + baseOverhead);
    }
}