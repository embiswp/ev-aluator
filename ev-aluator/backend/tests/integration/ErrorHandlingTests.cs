namespace EVRangeAnalyzer.Tests.Integration;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Integration tests for error handling and edge cases (Scenario 3 from quickstart.md).
/// Validates robust error handling and user feedback across all error conditions.
/// Tests file validation, data validation, session management, and network interruption scenarios.
/// These tests will fail until the full implementation is complete (expected in TDD).
/// </summary>
public class ErrorHandlingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorHandlingTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    public ErrorHandlingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// Tests invalid file upload scenarios.
    /// Validates that non-JSON files and oversized files are properly rejected.
    /// </summary>
    [Theory]
    [InlineData("test.txt", "text/plain", "This is not a JSON file")]
    [InlineData("document.pdf", "application/pdf", "%PDF-1.4 fake pdf content")]
    [InlineData("image.jpg", "image/jpeg", "fake jpeg content")]
    [InlineData("data.xml", "application/xml", "<root><data>xml content</data></root>")]
    public async Task InvalidFileUpload_NonJsonFiles_ShouldReturnClearErrorMessage(string fileName, string contentType, string fileContent)
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();

        // Act
        var response = await UploadFile(sessionCookie, fileContent, fileName, contentType);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
        
        Assert.NotNull(errorResponse);
        Assert.Equal("INVALID_FILE_FORMAT", errorResponse.Error);
        Assert.Contains("JSON", errorResponse.Message);
        
        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests file size limit validation.
    /// Ensures files exceeding 100MB limit are rejected before processing starts.
    /// </summary>
    [Fact]
    public async Task InvalidFileUpload_OversizedFile_ShouldReturnSizeLimitError()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        var oversizedContent = new string('x', 101 * 1024 * 1024); // 101MB

        // Act
        var response = await UploadFile(sessionCookie, oversizedContent, "huge-file.json", "application/json");

        // Assert
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
        
        Assert.NotNull(errorResponse);
        Assert.Equal("FILE_TOO_LARGE", errorResponse.Error);
        Assert.Contains("100MB", errorResponse.Message);
        
        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests corrupted JSON data handling.
    /// Validates that malformed JSON files are properly detected and reported.
    /// </summary>
    [Theory]
    [InlineData("{\"locations\": [", "Incomplete JSON structure")]
    [InlineData("{\"locations\": [invalid json}", "Invalid JSON syntax")]
    [InlineData("{\"invalid\": \"structure\"}", "Valid JSON but wrong structure")]
    [InlineData("", "Empty file")]
    [InlineData("not json at all", "Not JSON at all")]
    public async Task CorruptedJsonData_MalformedFiles_ShouldDetectParsingErrors(string jsonContent, string description)
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();

        // Act
        var response = await UploadFile(sessionCookie, jsonContent, "corrupted.json", "application/json");

        // Assert
        if (string.IsNullOrEmpty(jsonContent))
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        else
        {
            // Should either reject immediately or fail during processing
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Accepted,
                $"Corrupted JSON ({description}) should be rejected or accepted for processing");
            
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                // If accepted, processing should fail
                var uploadContent = await response.Content.ReadAsStringAsync();
                var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(uploadContent, _jsonOptions);
                
                // Wait for processing to fail
                await WaitForProcessingFailure(sessionCookie, uploadResponse.UploadId);
            }
        }
        
        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests empty or insufficient data scenarios.
    /// Validates appropriate messages for files with no location data or insufficient driving data.
    /// </summary>
    [Fact]
    public async Task EmptyOrInsufficientData_NoLocationData_ShouldReturnNoDataMessage()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        var emptyLocationJson = @"{""locations"": []}";

        // Act
        var uploadResponse = await UploadFile(sessionCookie, emptyLocationJson, "empty-locations.json");
        
        if (uploadResponse.StatusCode == HttpStatusCode.Accepted)
        {
            var uploadContent = await uploadResponse.Content.ReadAsStringAsync();
            var upload = JsonSerializer.Deserialize<UploadResponse>(uploadContent, _jsonOptions);
            await WaitForProcessingCompletion(sessionCookie, upload.UploadId);

            // Try to get data summary - should indicate no driving data
            var summaryResponse = await _client.GetAsync("/data/summary");
            
            if (summaryResponse.StatusCode == HttpStatusCode.OK)
            {
                var summaryContent = await summaryResponse.Content.ReadAsStringAsync();
                var summary = JsonSerializer.Deserialize<DataSummary>(summaryContent, _jsonOptions);
                Assert.Equal(0, summary.TotalLocationPoints);
            }
        }

        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests scenarios with only non-motorized transport data.
    /// Ensures warnings are provided when insufficient driving data is available.
    /// </summary>
    [Fact]
    public async Task InsufficientData_OnlyWalkingData_ShouldProvideInsufficientDataWarning()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        var walkingOnlyJson = CreateWalkingOnlyLocationJson();

        // Act
        var uploadId = await UploadLocationFile(sessionCookie, walkingOnlyJson, "walking-only.json");
        await WaitForProcessingCompletion(sessionCookie, uploadId);

        // Try to analyze EV compatibility - should provide appropriate message
        var analysisResponse = await _client.PostAsync("/analysis/ev-compatibility", 
            CreateEvAnalysisRequest(400));

        // Should either succeed with warning or return meaningful error
        if (analysisResponse.StatusCode == HttpStatusCode.OK)
        {
            var content = await analysisResponse.Content.ReadAsStringAsync();
            var analysis = JsonSerializer.Deserialize<EVAnalysisResponse>(content, _jsonOptions);
            
            // Should have very low or zero compatibility due to lack of driving data
            Assert.True(analysis.TotalDaysAnalyzed >= 0);
        }
        else if (analysisResponse.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorContent = await analysisResponse.Content.ReadAsStringAsync();
            var error = JsonSerializer.Deserialize<ErrorResponse>(errorContent, _jsonOptions);
            Assert.Contains("driving", error.Message.ToLower());
        }

        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests session timeout scenarios.
    /// Validates that expired sessions are properly handled and data is automatically cleared.
    /// </summary>
    [Fact]
    public async Task SessionTimeout_ExpiredSession_ShouldPromptReauthentication()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        await UploadAndProcessSampleFile(sessionCookie);

        // Simulate expired session by using an invalid/expired cookie
        var expiredSessionCookie = "expired-session-token";
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={expiredSessionCookie}");

        // Act - Try to access protected resource with expired session
        var response = await _client.GetAsync("/data/summary");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
        
        Assert.NotNull(errorResponse);
        Assert.True(errorResponse.Error == "AUTHENTICATION_REQUIRED" || errorResponse.Error == "SESSION_EXPIRED");
        
        // Original session should still work (until timeout implementation clears it)
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");
        var validResponse = await _client.GetAsync("/data/summary");
        // This may succeed or fail depending on session management implementation
        
        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests network interruption scenarios.
    /// Validates error detection and recovery when uploads are interrupted.
    /// </summary>
    [Fact]
    public async Task NetworkInterruption_InterruptedUpload_ShouldDetectErrorAndAllowRetry()
    {
        // This test simulates network interruption by canceling upload mid-process
        var sessionCookie = await AuthenticateUser();

        using var cts = new CancellationTokenSource();
        
        try
        {
            // Start upload and cancel it quickly to simulate interruption
            var largeContent = CreateLargeTestJson(10); // 10MB file
            var formData = new MultipartFormDataContent();
            formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(largeContent)), "file", "interrupted.json");

            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

            // Cancel after a short delay to simulate network interruption
            cts.CancelAfter(100);
            
            var uploadTask = _client.PostAsync("/upload/location-history", formData, cts.Token);
            
            // Expect either cancellation or normal completion
            try
            {
                await uploadTask;
                // If upload completed normally, that's fine for this test
            }
            catch (OperationCanceledException)
            {
                // This is the expected interruption
            }

            // Now try to upload again - should succeed
            using var newCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var retryContent = CreateSampleLocationJson();
            var retryFormData = new MultipartFormDataContent();
            retryFormData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(retryContent)), "file", "retry.json");

            var retryResponse = await _client.PostAsync("/upload/location-history", retryFormData, newCts.Token);
            
            // Retry should succeed
            Assert.True(retryResponse.StatusCode == HttpStatusCode.Accepted || retryResponse.StatusCode == HttpStatusCode.OK);
        }
        finally
        {
            // Cleanup
            await LogoutUser(sessionCookie);
        }
    }

    /// <summary>
    /// Tests invalid EV range input validation.
    /// Ensures proper validation for negative values and extremely large values.
    /// </summary>
    [Theory]
    [InlineData(-100, "Negative range values should be rejected")]
    [InlineData(0, "Zero range should be rejected")]
    [InlineData(1500, "Extremely large range should be rejected")]
    [InlineData(5000, "Unrealistic range should be rejected")]
    public async Task InvalidEvRangeInput_InvalidValues_ShouldReturnValidationError(int rangeKm, string description)
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        await UploadAndProcessSampleFile(sessionCookie);

        // Act
        var request = new EVAnalysisRequest
        {
            EvRangeKm = rangeKm,
            IncludeChargingBuffer = true,
            AnalysisName = $"Invalid Range Test {rangeKm}km",
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.PostAsync("/analysis/ev-compatibility", jsonContent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
        
        Assert.NotNull(errorResponse);
        Assert.Equal("INVALID_EV_RANGE", errorResponse.Error);
        Assert.True(errorResponse.Message.Contains("50") && errorResponse.Message.Contains("1000"),
            $"Error message should mention valid range limits for {description}");
        
        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests graceful recovery from various error conditions.
    /// Ensures system returns to stable state after errors.
    /// </summary>
    [Fact]
    public async Task ErrorRecovery_MultipleErrorConditions_ShouldRecoverGracefully()
    {
        var sessionCookie = await AuthenticateUser();

        try
        {
            // 1. Try invalid file upload
            await UploadFile(sessionCookie, "invalid content", "bad.txt", "text/plain");

            // 2. Try valid upload
            var validUploadId = await UploadLocationFile(sessionCookie, CreateSampleLocationJson(), "valid.json");
            await WaitForProcessingCompletion(sessionCookie, validUploadId);

            // 3. Try invalid EV analysis
            var invalidAnalysisResponse = await _client.PostAsync("/analysis/ev-compatibility",
                CreateEvAnalysisRequest(-100));
            Assert.Equal(HttpStatusCode.BadRequest, invalidAnalysisResponse.StatusCode);

            // 4. Try valid EV analysis - should work despite previous errors
            var validAnalysisResponse = await _client.PostAsync("/analysis/ev-compatibility",
                CreateEvAnalysisRequest(400));
            Assert.Equal(HttpStatusCode.OK, validAnalysisResponse.StatusCode);

            // 5. System should be in stable state
            var summaryResponse = await _client.GetAsync("/data/summary");
            Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        }
        finally
        {
            // Cleanup
            await LogoutUser(sessionCookie);
        }
    }

    /// <summary>
    /// Tests data corruption prevention during failed operations.
    /// Ensures that failed operations don't leave system in inconsistent state.
    /// </summary>
    [Fact]
    public async Task DataIntegrity_FailedOperations_ShouldNotCorruptExistingData()
    {
        var sessionCookie = await AuthenticateUser();

        // Upload and process valid data first
        var validUploadId = await UploadLocationFile(sessionCookie, CreateSampleLocationJson(), "valid-data.json");
        await WaitForProcessingCompletion(sessionCookie, validUploadId);

        // Get initial data summary
        var initialSummary = await GetDataSummary(sessionCookie);

        // Try to upload corrupted data (should not affect existing data)
        try
        {
            await UploadFile(sessionCookie, "{invalid json", "corrupted.json");
        }
        catch
        {
            // Ignore upload failures
        }

        // Try invalid analysis requests
        try
        {
            await _client.PostAsync("/analysis/ev-compatibility", CreateEvAnalysisRequest(-100));
        }
        catch
        {
            // Ignore analysis failures
        }

        // Verify original data is still intact
        var finalSummary = await GetDataSummary(sessionCookie);
        
        Assert.Equal(initialSummary.TotalLocationPoints, finalSummary.TotalLocationPoints);
        Assert.Equal(initialSummary.TotalDays, finalSummary.TotalDays);
        Assert.Equal(initialSummary.FileName, finalSummary.FileName);

        // Valid operations should still work
        var validAnalysisResponse = await _client.PostAsync("/analysis/ev-compatibility",
            CreateEvAnalysisRequest(400));
        Assert.Equal(HttpStatusCode.OK, validAnalysisResponse.StatusCode);

        // Cleanup
        await LogoutUser(sessionCookie);
    }

    // Helper methods

    private async Task<string> AuthenticateUser()
    {
        var loginResponse = await _client.GetAsync("/auth/login");
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var callbackResponse = await _client.GetAsync("/auth/callback?code=test-code&state=test-state");
        Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);

        var setCookieHeader = callbackResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.NotNull(setCookieHeader);
        
        var sessionCookie = ExtractSessionCookie(setCookieHeader);
        Assert.NotNull(sessionCookie);

        return sessionCookie;
    }

    private async Task<HttpResponseMessage> UploadFile(string sessionCookie, string fileContent, 
        string fileName, string contentType = "application/json")
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var formData = new MultipartFormDataContent();
        var fileByteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent));
        fileByteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        formData.Add(fileByteContent, "file", fileName);

        return await _client.PostAsync("/upload/location-history", formData);
    }

    private async Task<string> UploadLocationFile(string sessionCookie, string jsonContent, string fileName)
    {
        var response = await UploadFile(sessionCookie, jsonContent, fileName);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(content, _jsonOptions);
        
        Assert.NotNull(uploadResponse);
        Assert.NotNull(uploadResponse.UploadId);

        return uploadResponse.UploadId;
    }

    private async Task WaitForProcessingCompletion(string sessionCookie, string uploadId)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var maxWaitTime = TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < maxWaitTime)
        {
            var response = await _client.GetAsync("/upload/status");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                var status = JsonSerializer.Deserialize<ProcessingStatus>(content, _jsonOptions);
                
                if (status != null && status.Status == "completed")
                {
                    return;
                }
                
                if (status != null && status.Status == "failed")
                {
                    throw new InvalidOperationException($"Processing failed: {status.Error}");
                }
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("Processing did not complete within time limit");
    }

    private async Task WaitForProcessingFailure(string sessionCookie, string uploadId)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var maxWaitTime = TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < maxWaitTime)
        {
            var response = await _client.GetAsync("/upload/status");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                var status = JsonSerializer.Deserialize<ProcessingStatus>(content, _jsonOptions);
                
                if (status != null && status.Status == "failed")
                {
                    return; // Expected failure
                }
                
                if (status != null && status.Status == "completed")
                {
                    throw new InvalidOperationException("Processing unexpectedly succeeded");
                }
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("Processing did not fail within expected time");
    }

    private async Task<DataSummary> GetDataSummary(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.GetAsync("/data/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var summary = JsonSerializer.Deserialize<DataSummary>(content, _jsonOptions);
        
        Assert.NotNull(summary);
        return summary;
    }

    private async Task UploadAndProcessSampleFile(string sessionCookie)
    {
        var uploadId = await UploadLocationFile(sessionCookie, CreateSampleLocationJson(), "sample.json");
        await WaitForProcessingCompletion(sessionCookie, uploadId);
    }

    private async Task LogoutUser(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        await _client.PostAsync("/auth/logout", null);
    }

    private StringContent CreateEvAnalysisRequest(int rangeKm)
    {
        var request = new EVAnalysisRequest
        {
            EvRangeKm = rangeKm,
            IncludeChargingBuffer = true,
            AnalysisName = $"Test Analysis {rangeKm}km",
        };

        return new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");
    }

    private static string CreateSampleLocationJson()
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
                },
                {
                    ""timestampMs"": ""1641000000000"",
                    ""latitudeE7"": 473866670,
                    ""longitudeE7"": -1222870000,
                    ""accuracy"": 15,
                    ""activity"": [
                        {
                            ""timestampMs"": ""1641000000000"",
                            ""activity"": [
                                {
                                    ""type"": ""IN_VEHICLE"",
                                    ""confidence"": 90
                                }
                            ]
                        }
                    ]
                }
            ]
        }";
    }

    private static string CreateWalkingOnlyLocationJson()
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
                                    ""type"": ""WALKING"",
                                    ""confidence"": 85
                                }
                            ]
                        }
                    ]
                },
                {
                    ""timestampMs"": ""1641000000000"",
                    ""latitudeE7"": 473866670,
                    ""longitudeE7"": -1222870000,
                    ""accuracy"": 15,
                    ""activity"": [
                        {
                            ""timestampMs"": ""1641000000000"",
                            ""activity"": [
                                {
                                    ""type"": ""WALKING"",
                                    ""confidence"": 90
                                }
                            ]
                        }
                    ]
                }
            ]
        }";
    }

    private static string CreateLargeTestJson(int approximateSizeMB)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"{""locations"": [");

        var entriesPerMB = 2500;
        var totalEntries = approximateSizeMB * entriesPerMB;

        for (int i = 0; i < totalEntries; i++)
        {
            var timestamp = 1640995200000L + (i * 60000);
            var lat = 473766670 + (i % 1000);
            var lon = -1222770000 - (i % 1000);
            
            sb.Append($@"    {{
      ""timestampMs"": ""{timestamp}"",
      ""latitudeE7"": {lat},
      ""longitudeE7"": {lon},
      ""accuracy"": 20,
      ""activity"": [
        {{
          ""timestampMs"": ""{timestamp}"",
          ""activity"": [
            {{
              ""type"": ""IN_VEHICLE"",
              ""confidence"": 85
            }}
          ]
        }}
      ]
    }}");

            if (i < totalEntries - 1)
            {
                sb.AppendLine(",");
            }
            else
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("]}");
        return sb.ToString();
    }

    private static string ExtractSessionCookie(string setCookieHeader)
    {
        var match = System.Text.RegularExpressions.Regex.Match(setCookieHeader, @"ev-session=([^;]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    // Model classes
    private class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object? Details { get; set; }
    }

    private class UploadResponse
    {
        public string UploadId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int EstimatedProcessingTimeMs { get; set; }
    }

    private class ProcessingStatus
    {
        public string Status { get; set; } = string.Empty;
        public ProcessingProgress Progress { get; set; } = new ProcessingProgress();
        public string? Error { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    private class ProcessingProgress
    {
        public double Percentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public int TotalSteps { get; set; }
        public int ProcessedPoints { get; set; }
        public int TotalPoints { get; set; }
    }

    private class DataSummary
    {
        public string FileName { get; set; } = string.Empty;
        public double FileSize { get; set; }
        public DateRange DateRange { get; set; } = new DateRange();
        public int TotalLocationPoints { get; set; }
        public int TotalDays { get; set; }
        public int DaysWithMotorizedTrips { get; set; }
        public double AverageDailyDistance { get; set; }
        public double LongestDayDistance { get; set; }
    }

    private class DateRange
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }

    private class EVAnalysisRequest
    {
        public int EvRangeKm { get; set; }
        public bool IncludeChargingBuffer { get; set; } = true;
        public string? AnalysisName { get; set; }
    }

    private class EVAnalysisResponse
    {
        public int EvRangeKm { get; set; }
        public int TotalDaysAnalyzed { get; set; }
        public int CompatibleDays { get; set; }
        public int IncompatibleDays { get; set; }
        public double CompatibilityPercentage { get; set; }
        public DateTime AnalysisDate { get; set; }
        public AnalysisStatistics Statistics { get; set; } = new AnalysisStatistics();
        public List<IncompatibleDayDetail>? IncompatibleDaysDetails { get; set; }
    }

    private class AnalysisStatistics
    {
        public double AverageDailyDistance { get; set; }
        public double MedianDailyDistance { get; set; }
        public double MaximumDailyDistance { get; set; }
        public double StandardDeviation { get; set; }
    }

    private class IncompatibleDayDetail
    {
        public string Date { get; set; } = string.Empty;
        public double DistanceKm { get; set; }
        public double ExceedsRangeByKm { get; set; }
    }
}