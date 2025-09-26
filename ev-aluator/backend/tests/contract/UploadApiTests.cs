namespace EVRangeAnalyzer.Tests.Contract;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Contract tests for file upload endpoints as specified in upload-api.yaml.
/// These tests verify the API contract compliance for location history file processing.
/// </summary>
public class UploadApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadApiTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    public UploadApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Tests POST /upload/location-history endpoint contract with valid JSON file.
    /// Should return 202 Accepted with upload response.
    /// </summary>
    [Fact]
    public async Task PostUploadLocationHistory_WithValidFile_ShouldReturn202()
    {
        // Arrange
        await SetupAuthenticatedSession();
        var fileContent = CreateMockGoogleTakeoutJson();
        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent)), "file", "Location History.json");

        // Act
        var response = await _client.PostAsync("/upload/location-history", formData);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(uploadResponse);
        Assert.NotNull(uploadResponse.UploadId);
        Assert.Equal("uploading", uploadResponse.Status);
        Assert.Equal("File upload started", uploadResponse.Message);
        Assert.True(uploadResponse.EstimatedProcessingTimeMs > 0);
    }

    /// <summary>
    /// Tests POST /upload/location-history with file exceeding 100MB limit.
    /// Should return 413 Payload Too Large.
    /// </summary>
    [Fact]
    public async Task PostUploadLocationHistory_WithOversizedFile_ShouldReturn413()
    {
        // Arrange
        await SetupAuthenticatedSession();
        var largeFileContent = new string('x', 101 * 1024 * 1024); // 101MB
        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(largeFileContent)), "file", "large-file.json");

        // Act
        var response = await _client.PostAsync("/upload/location-history", formData);

        // Assert
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(errorResponse);
        Assert.Equal("FILE_TOO_LARGE", errorResponse.Error);
        Assert.Contains("100MB limit", errorResponse.Message);
    }

    /// <summary>
    /// Tests POST /upload/location-history with invalid file format.
    /// Should return 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task PostUploadLocationHistory_WithInvalidFileFormat_ShouldReturn400()
    {
        // Arrange
        await SetupAuthenticatedSession();
        var invalidContent = "This is not JSON content";
        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(invalidContent)), "file", "invalid.txt");

        // Act
        var response = await _client.PostAsync("/upload/location-history", formData);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(errorResponse);
        Assert.Equal("INVALID_FILE_FORMAT", errorResponse.Error);
    }

    /// <summary>
    /// Tests POST /upload/location-history without authentication.
    /// Should return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task PostUploadLocationHistory_WithoutAuthentication_ShouldReturn401()
    {
        // Arrange
        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("{}")), "file", "test.json");

        // Act
        var response = await _client.PostAsync("/upload/location-history", formData);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests POST /upload/location-history when data already exists without replaceExisting flag.
    /// Should return 409 Conflict.
    /// </summary>
    [Fact]
    public async Task PostUploadLocationHistory_WithExistingDataAndNoReplace_ShouldReturn409()
    {
        // Arrange
        await SetupAuthenticatedSession();
        await UploadInitialFile(); // First upload

        var fileContent = CreateMockGoogleTakeoutJson();
        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent)), "file", "Location History.json");
        formData.Add(new StringContent("false"), "replaceExisting");

        // Act
        var response = await _client.PostAsync("/upload/location-history", formData);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(errorResponse);
        Assert.Equal("DATA_ALREADY_EXISTS", errorResponse.Error);
    }

    /// <summary>
    /// Tests GET /upload/status endpoint contract.
    /// Should return 200 OK with processing status.
    /// </summary>
    [Fact]
    public async Task GetUploadStatus_WithActiveUpload_ShouldReturn200WithStatus()
    {
        // Arrange
        await SetupAuthenticatedSession();
        await UploadInitialFile();

        // Act
        var response = await _client.GetAsync("/upload/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var processingStatus = JsonSerializer.Deserialize<ProcessingStatus>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(processingStatus);
        Assert.Contains(processingStatus.Status, new[] { "uploading", "parsing", "processing", "completed", "failed" });
        Assert.NotNull(processingStatus.Progress);
        Assert.True(processingStatus.Progress.Percentage >= 0 && processingStatus.Progress.Percentage <= 100);
    }

    /// <summary>
    /// Tests GET /upload/status without any upload.
    /// Should return 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetUploadStatus_WithoutUpload_ShouldReturn404()
    {
        // Arrange
        await SetupAuthenticatedSession();

        // Act
        var response = await _client.GetAsync("/upload/status");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(errorResponse);
        Assert.Equal("NO_UPLOAD_FOUND", errorResponse.Error);
    }

    /// <summary>
    /// Tests GET /data/summary endpoint contract.
    /// Should return 200 OK with data summary.
    /// </summary>
    [Fact]
    public async Task GetDataSummary_WithProcessedData_ShouldReturn200WithSummary()
    {
        // Arrange
        await SetupAuthenticatedSession();
        await UploadAndProcessFile();

        // Act
        var response = await _client.GetAsync("/data/summary");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var dataSummary = JsonSerializer.Deserialize<DataSummary>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(dataSummary);
        Assert.Equal("Location History.json", dataSummary.FileName);
        Assert.True(dataSummary.FileSize > 0);
        Assert.NotNull(dataSummary.DateRange);
        Assert.True(dataSummary.TotalLocationPoints > 0);
        Assert.True(dataSummary.TotalDays > 0);
    }

    /// <summary>
    /// Tests GET /data/summary without processed data.
    /// Should return 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetDataSummary_WithoutProcessedData_ShouldReturn404()
    {
        // Arrange
        await SetupAuthenticatedSession();

        // Act
        var response = await _client.GetAsync("/data/summary");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Tests DELETE /data endpoint contract.
    /// Should return 200 OK with delete response.
    /// </summary>
    [Fact]
    public async Task DeleteData_WithExistingData_ShouldReturn200WithDeleteResponse()
    {
        // Arrange
        await SetupAuthenticatedSession();
        await UploadAndProcessFile();

        // Act
        var response = await _client.DeleteAsync("/data");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var deleteResponse = JsonSerializer.Deserialize<DeleteResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(deleteResponse);
        Assert.True(deleteResponse.Success);
        Assert.Equal("Location data deleted successfully", deleteResponse.Message);
        Assert.NotNull(deleteResponse.DeletedItems);
        Assert.True(deleteResponse.DeletedItems.LocationPoints > 0);
    }

    /// <summary>
    /// Tests DELETE /data without existing data.
    /// Should return 404 Not Found.
    /// </summary>
    [Fact]
    public async Task DeleteData_WithoutExistingData_ShouldReturn404()
    {
        // Arrange
        await SetupAuthenticatedSession();

        // Act
        var response = await _client.DeleteAsync("/data");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Creates mock Google Takeout JSON content for testing.
    /// </summary>
    /// <returns>Mock JSON content representing Google location history.</returns>
    private static string CreateMockGoogleTakeoutJson()
    {
        return @"{
            ""locations"": [
                {
                    ""timestampMs"": ""1640995200000"",
                    ""latitudeE7"": 473766670,
                    ""longitudeE7"": -1222770000,
                    ""accuracy"": 20,
                    ""activity"": [
                        {
                            ""timestampMs"": ""1640995200000"",
                            ""activity"": [
                                {
                                    ""type"": ""IN_VEHICLE"",
                                    ""confidence"": 85
                                }
                            ]
                        }
                    ]
                }
            ]
        }";
    }

    /// <summary>
    /// Sets up an authenticated session for testing purposes.
    /// This will fail until auth implementation is completed (expected in TDD).
    /// </summary>
    private async Task SetupAuthenticatedSession()
    {
        // Mock session setup - will fail until implemented
        _client.DefaultRequestHeaders.Add("Cookie", "ev-session=mock-authenticated-session");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Uploads an initial file for testing purposes.
    /// This will fail until upload implementation is completed (expected in TDD).
    /// </summary>
    private async Task UploadInitialFile()
    {
        var fileContent = CreateMockGoogleTakeoutJson();
        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent)), "file", "Location History.json");

        // This will fail until implemented - that's expected in TDD
        await _client.PostAsync("/upload/location-history", formData);
    }

    /// <summary>
    /// Uploads and processes a file for testing purposes.
    /// This will fail until implementation is completed (expected in TDD).
    /// </summary>
    private async Task UploadAndProcessFile()
    {
        await UploadInitialFile();

        // In actual implementation, would wait for processing to complete
    }

    // Contract models matching upload-api.yaml specifications

    /// <summary>
    /// Contract model for error responses.
    /// </summary>
    private class ErrorResponse
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional error details.
        /// </summary>
        public object? Details { get; set; }
    }

    /// <summary>
    /// Contract model for upload response.
    /// </summary>
    private class UploadResponse
    {
        /// <summary>
        /// Gets or sets the upload identifier.
        /// </summary>
        public string UploadId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the processing status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the estimated processing time in milliseconds.
        /// </summary>
        public int EstimatedProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Contract model for processing status.
    /// </summary>
    private class ProcessingStatus
    {
        /// <summary>
        /// Gets or sets the current status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the progress information.
        /// </summary>
        public ProcessingProgress Progress { get; set; } = new ProcessingProgress();

        /// <summary>
        /// Gets or sets the error message if failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets when processing started.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gets or sets when processing completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Contract model for processing progress.
    /// </summary>
    private class ProcessingProgress
    {
        /// <summary>
        /// Gets or sets the completion percentage.
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Gets or sets the current step description.
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of steps.
        /// </summary>
        public int TotalSteps { get; set; }

        /// <summary>
        /// Gets or sets the number of processed points.
        /// </summary>
        public int ProcessedPoints { get; set; }

        /// <summary>
        /// Gets or sets the total number of points.
        /// </summary>
        public int TotalPoints { get; set; }
    }

    /// <summary>
    /// Contract model for data summary.
    /// </summary>
    private class DataSummary
    {
        /// <summary>
        /// Gets or sets the original file name.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in MB.
        /// </summary>
        public double FileSize { get; set; }

        /// <summary>
        /// Gets or sets the date range of the data.
        /// </summary>
        public DateRange DateRange { get; set; } = new DateRange();

        /// <summary>
        /// Gets or sets the total number of location points.
        /// </summary>
        public int TotalLocationPoints { get; set; }

        /// <summary>
        /// Gets or sets the total number of days.
        /// </summary>
        public int TotalDays { get; set; }

        /// <summary>
        /// Gets or sets the number of days with motorized trips.
        /// </summary>
        public int DaysWithMotorizedTrips { get; set; }

        /// <summary>
        /// Gets or sets the average daily distance in km.
        /// </summary>
        public double AverageDailyDistance { get; set; }

        /// <summary>
        /// Gets or sets the longest single day distance in km.
        /// </summary>
        public double LongestDayDistance { get; set; }
    }

    /// <summary>
    /// Contract model for date range.
    /// </summary>
    private class DateRange
    {
        /// <summary>
        /// Gets or sets the start date.
        /// </summary>
        public DateOnly StartDate { get; set; }

        /// <summary>
        /// Gets or sets the end date.
        /// </summary>
        public DateOnly EndDate { get; set; }
    }

    /// <summary>
    /// Contract model for delete response.
    /// </summary>
    private class DeleteResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether deletion was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the deletion message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets information about deleted items.
        /// </summary>
        public DeletedItems DeletedItems { get; set; } = new DeletedItems();
    }

    /// <summary>
    /// Contract model for deleted items information.
    /// </summary>
    private class DeletedItems
    {
        /// <summary>
        /// Gets or sets the number of deleted location points.
        /// </summary>
        public int LocationPoints { get; set; }

        /// <summary>
        /// Gets or sets the number of deleted daily summaries.
        /// </summary>
        public int DailySummaries { get; set; }
    }
}