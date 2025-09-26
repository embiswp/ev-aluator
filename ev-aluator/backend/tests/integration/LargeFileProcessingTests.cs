namespace EVRangeAnalyzer.Tests.Integration;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Integration tests for large file processing (Scenario 2 from quickstart.md).
/// Validates system performance with maximum file size (up to 100MB).
/// Tests processing time limits, memory usage, and API response times.
/// These tests will fail until the full implementation is complete (expected in TDD).
/// </summary>
public class LargeFileProcessingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="LargeFileProcessingTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    public LargeFileProcessingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// Tests upload and processing of large files (80-100MB).
    /// Validates that processing completes within 2 minutes.
    /// </summary>
    [Fact]
    public async Task LargeFileProcessing_NearMaximumSize_ShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        var largeFileContent = CreateLargeGoogleTakeoutJson(80); // 80MB test file

        // Act - Upload large file
        var uploadStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var uploadId = await UploadLargeFile(sessionCookie, largeFileContent, "Large Location History.json");
        uploadStopwatch.Stop();

        // Assert - Upload should succeed without size errors
        Assert.NotNull(uploadId);
        
        // Act - Monitor processing performance
        var processingStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await WaitForProcessingCompletion(sessionCookie, uploadId);
        processingStopwatch.Stop();

        // Assert - Processing should complete within 2 minutes
        Assert.True(processingStopwatch.Elapsed.TotalMinutes < 2,
            $"Large file processing took {processingStopwatch.Elapsed.TotalMinutes:F2} minutes, should be under 2 minutes");

        // Cleanup
        await DeleteUserData(sessionCookie);
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests API response times with large datasets.
    /// Ensures all API responses remain under 200ms requirement.
    /// </summary>
    [Fact]
    public async Task LargeFileProcessing_ApiResponseTimes_ShouldMeetPerformanceRequirements()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        var largeFileContent = CreateLargeGoogleTakeoutJson(50); // 50MB for faster test execution
        var uploadId = await UploadLargeFile(sessionCookie, largeFileContent, "Performance Test File.json");
        await WaitForProcessingCompletion(sessionCookie, uploadId);

        // Test data summary API response time
        var summaryStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await GetDataSummary(sessionCookie);
        summaryStopwatch.Stop();

        Assert.True(summaryStopwatch.Elapsed.TotalMilliseconds < 200,
            $"Data summary API took {summaryStopwatch.Elapsed.TotalMilliseconds:F0}ms, should be under 200ms");

        // Test EV analysis API response time
        var analysisStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await AnalyzeEVCompatibility(sessionCookie, 400);
        analysisStopwatch.Stop();

        Assert.True(analysisStopwatch.Elapsed.TotalMilliseconds < 200,
            $"EV analysis API took {analysisStopwatch.Elapsed.TotalMilliseconds:F0}ms, should be under 200ms");

        // Test daily distances API response time
        var dailyDistancesStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await GetDailyDistanceBreakdown(sessionCookie);
        dailyDistancesStopwatch.Stop();

        Assert.True(dailyDistancesStopwatch.Elapsed.TotalMilliseconds < 200,
            $"Daily distances API took {dailyDistancesStopwatch.Elapsed.TotalMilliseconds:F0}ms, should be under 200ms");

        // Test statistics API response time
        var statisticsStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await GetStatistics(sessionCookie);
        statisticsStopwatch.Stop();

        Assert.True(statisticsStopwatch.Elapsed.TotalMilliseconds < 200,
            $"Statistics API took {statisticsStopwatch.Elapsed.TotalMilliseconds:F0}ms, should be under 200ms");

        // Cleanup
        await DeleteUserData(sessionCookie);
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests memory stability during large file processing.
    /// Monitors for memory leaks and ensures stable memory usage.
    /// </summary>
    [Fact]
    public async Task LargeFileProcessing_MemoryUsage_ShouldRemainStable()
    {
        // Get initial memory baseline
        var initialMemory = GC.GetTotalMemory(true);

        // Process multiple large files to test for memory leaks
        for (int i = 0; i < 3; i++)
        {
            var sessionCookie = await AuthenticateUser();
            var largeFileContent = CreateLargeGoogleTakeoutJson(30); // 30MB per iteration
            
            var uploadId = await UploadLargeFile(sessionCookie, largeFileContent, $"MemoryTest{i}.json");
            await WaitForProcessingCompletion(sessionCookie, uploadId);
            
            // Perform some operations
            await GetDataSummary(sessionCookie);
            await AnalyzeEVCompatibility(sessionCookie, 400);
            
            // Cleanup
            await DeleteUserData(sessionCookie);
            await LogoutUser(sessionCookie);

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Check final memory usage
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024 * 1024);

        // Memory increase should be reasonable (less than 100MB after processing 90MB total)
        Assert.True(memoryIncreaseMB < 100,
            $"Memory increased by {memoryIncreaseMB:F2}MB after processing large files, should be under 100MB");
    }

    /// <summary>
    /// Tests concurrent large file processing.
    /// Ensures system can handle multiple large file uploads efficiently.
    /// </summary>
    [Fact]
    public async Task LargeFileProcessing_ConcurrentUploads_ShouldHandleMultipleFiles()
    {
        // Arrange - Create multiple sessions
        var sessions = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            sessions.Add(await AuthenticateUser());
        }

        // Act - Upload large files concurrently
        var uploadTasks = new List<Task<string>>();
        for (int i = 0; i < sessions.Count; i++)
        {
            var sessionCookie = sessions[i];
            var fileContent = CreateLargeGoogleTakeoutJson(25); // 25MB each
            uploadTasks.Add(UploadLargeFile(sessionCookie, fileContent, $"Concurrent{i}.json"));
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var uploadIds = await Task.WhenAll(uploadTasks);
        stopwatch.Stop();

        // Assert - All uploads should succeed
        Assert.True(uploadIds.All(id => !string.IsNullOrEmpty(id)),
            "All concurrent uploads should succeed");

        // Wait for all processing to complete
        var processingTasks = new List<Task>();
        for (int i = 0; i < sessions.Count; i++)
        {
            processingTasks.Add(WaitForProcessingCompletion(sessions[i], uploadIds[i]));
        }

        var processingStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Task.WhenAll(processingTasks);
        processingStopwatch.Stop();

        // Processing should complete efficiently even with concurrent files
        Assert.True(processingStopwatch.Elapsed.TotalMinutes < 5,
            $"Concurrent processing took {processingStopwatch.Elapsed.TotalMinutes:F2} minutes, should be under 5 minutes");

        // Cleanup all sessions
        for (int i = 0; i < sessions.Count; i++)
        {
            await DeleteUserData(sessions[i]);
            await LogoutUser(sessions[i]);
        }
    }

    /// <summary>
    /// Tests processing accuracy with large datasets.
    /// Ensures analysis results remain accurate regardless of file size.
    /// </summary>
    [Fact]
    public async Task LargeFileProcessing_AnalysisAccuracy_ShouldMaintainAccuracyWithLargeDatasets()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        var largeFileContent = CreateDetailedGoogleTakeoutJson(1000); // 1000 location points
        
        var uploadId = await UploadLargeFile(sessionCookie, largeFileContent, "Accuracy Test.json");
        await WaitForProcessingCompletion(sessionCookie, uploadId);

        // Act - Perform various analyses
        var analysis200km = await AnalyzeEVCompatibility(sessionCookie, 200);
        var analysis400km = await AnalyzeEVCompatibility(sessionCookie, 400);
        var analysis600km = await AnalyzeEVCompatibility(sessionCookie, 600);
        var dailyDistances = await GetDailyDistanceBreakdown(sessionCookie);
        var statistics = await GetStatistics(sessionCookie);

        // Assert - Results should be logically consistent
        Assert.True(analysis200km.CompatibilityPercentage <= analysis400km.CompatibilityPercentage,
            "200km range should have equal or lower compatibility than 400km");
            
        Assert.True(analysis400km.CompatibilityPercentage <= analysis600km.CompatibilityPercentage,
            "400km range should have equal or lower compatibility than 600km");

        // Total days should be consistent across all results
        Assert.Equal(analysis200km.TotalDaysAnalyzed, analysis400km.TotalDaysAnalyzed);
        Assert.Equal(analysis400km.TotalDaysAnalyzed, analysis600km.TotalDaysAnalyzed);
        Assert.Equal(dailyDistances.TotalDays, analysis400km.TotalDaysAnalyzed);

        // Statistics should be reasonable
        Assert.True(statistics.TotalDistanceKm > 0);
        Assert.True(statistics.AverageDailyDistance >= 0);
        Assert.True(statistics.MaximumDailyDistance >= statistics.AverageDailyDistance);
        Assert.True(statistics.MinimumDailyDistance <= statistics.AverageDailyDistance);

        // Cleanup
        await DeleteUserData(sessionCookie);
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests UI responsiveness during large file processing.
    /// Ensures the system remains responsive while processing large files.
    /// </summary>
    [Fact]
    public async Task LargeFileProcessing_UIResponsiveness_ShouldMaintainResponsivenessDuringProcessing()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        var largeFileContent = CreateLargeGoogleTakeoutJson(60); // 60MB file
        
        // Start upload and processing
        var uploadId = await UploadLargeFile(sessionCookie, largeFileContent, "UI Test.json");

        // While processing is ongoing, test API responsiveness
        var responsivenessTasks = new List<Task>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Make multiple status check calls (simulating UI polling)
        for (int i = 0; i < 10; i++)
        {
            responsivenessTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(i * 1000); // Stagger requests
                var statusStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _client.GetAsync("/upload/status");
                statusStopwatch.Stop();
                
                // Each status check should be fast even during processing
                Assert.True(statusStopwatch.Elapsed.TotalMilliseconds < 500,
                    $"Status check took {statusStopwatch.Elapsed.TotalMilliseconds:F0}ms during processing, should be under 500ms");
            }));
        }

        // Wait for all responsiveness tests to complete
        await Task.WhenAll(responsivenessTasks);
        
        // Wait for processing to complete
        await WaitForProcessingCompletion(sessionCookie, uploadId);
        stopwatch.Stop();

        // System should remain responsive throughout
        Assert.True(stopwatch.Elapsed.TotalMinutes < 3,
            $"Large file processing with UI responsiveness tests took {stopwatch.Elapsed.TotalMinutes:F2} minutes");

        // Cleanup
        await DeleteUserData(sessionCookie);
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

    private async Task<string> UploadLargeFile(string sessionCookie, string fileContent, string fileName)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent)), "file", fileName);

        var response = await _client.PostAsync("/upload/location-history", formData);
        
        // Should accept the file without size errors
        Assert.True(response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.OK,
            $"Large file upload failed with status {response.StatusCode}");

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

        var maxWaitTime = TimeSpan.FromMinutes(3); // Allow extra time for large files
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < maxWaitTime)
        {
            var response = await _client.GetAsync("/upload/status");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                var status = JsonSerializer.Deserialize<ProcessingStatus>(content, _jsonOptions);
                
                if (status != null)
                {
                    if (status.Status == "completed")
                    {
                        return;
                    }
                    else if (status.Status == "failed")
                    {
                        Assert.True(false, $"File processing failed: {status.Error}");
                    }
                }
            }

            await Task.Delay(2000); // Check every 2 seconds for large files
        }

        Assert.True(false, "Large file processing did not complete within time limit");
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

    private async Task<EVAnalysisResponse> AnalyzeEVCompatibility(string sessionCookie, int rangeKm)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var request = new EVAnalysisRequest
        {
            EvRangeKm = rangeKm,
            IncludeChargingBuffer = true,
            AnalysisName = $"Large File Test {rangeKm}km",
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/analysis/ev-compatibility", jsonContent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var analysis = JsonSerializer.Deserialize<EVAnalysisResponse>(content, _jsonOptions);
        
        Assert.NotNull(analysis);
        return analysis;
    }

    private async Task<DailyDistancesResponse> GetDailyDistanceBreakdown(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.GetAsync("/analysis/daily-distances?limit=50&sortBy=distance&sortOrder=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var dailyDistances = JsonSerializer.Deserialize<DailyDistancesResponse>(content, _jsonOptions);
        
        Assert.NotNull(dailyDistances);
        return dailyDistances;
    }

    private async Task<DrivingStatistics> GetStatistics(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.GetAsync("/analysis/statistics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var statistics = JsonSerializer.Deserialize<DrivingStatistics>(content, _jsonOptions);
        
        Assert.NotNull(statistics);
        return statistics;
    }

    private async Task DeleteUserData(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.DeleteAsync("/data");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task LogoutUser(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string CreateLargeGoogleTakeoutJson(int approximateSizeMB)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"{""locations"": [");

        // Calculate approximate number of entries needed
        // Each entry is roughly 300-400 bytes, so ~2500-3000 entries per MB
        var entriesPerMB = 2500;
        var totalEntries = approximateSizeMB * entriesPerMB;

        for (int i = 0; i < totalEntries; i++)
        {
            var timestamp = 1640995200000L + (i * 60000); // 1 minute intervals
            var lat = 473766670 + (i % 1000); // Vary location slightly
            var lon = -1222770000 - (i % 1000);
            
            sb.Append(@$"    {{
      ""timestampMs"": ""{timestamp}"",
      ""latitudeE7"": {lat},
      ""longitudeE7"": {lon},
      ""accuracy"": {20 + (i % 30)},
      ""activity"": [
        {{
          ""timestampMs"": ""{timestamp}"",
          ""activity"": [
            {{
              ""type"": ""IN_VEHICLE"",
              ""confidence"": {70 + (i % 25)}
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

            // Add some variety in transport modes
            if (i % 10 == 0)
            {
                sb.AppendLine(@"    },
    {
      ""timestampMs"": """ + (timestamp + 30000) + @""",
      ""latitudeE7"": " + (lat + 10) + @",
      ""longitudeE7"": " + (lon - 10) + @",
      ""accuracy"": 15,
      ""activity"": [
        {
          ""timestampMs"": """ + (timestamp + 30000) + @""",
          ""activity"": [
            {
              ""type"": ""WALKING"",
              ""confidence"": 80
            }
          ]
        }
      ]");
            }
        }

        sb.AppendLine("]}");
        return sb.ToString();
    }

    private static string CreateDetailedGoogleTakeoutJson(int locationPointCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"{""locations"": [");

        for (int i = 0; i < locationPointCount; i++)
        {
            var timestamp = 1640995200000L + (i * 300000); // 5 minute intervals
            var lat = 473766670 + (i % 10000); // More variation in location
            var lon = -1222770000 - (i % 10000);
            
            // Create realistic driving patterns
            var transportType = i % 20 < 15 ? "IN_VEHICLE" : "WALKING";
            var confidence = transportType == "IN_VEHICLE" ? 80 + (i % 15) : 70 + (i % 20);
            var accuracy = transportType == "IN_VEHICLE" ? 15 + (i % 10) : 25 + (i % 15);

            sb.Append(@$"    {{
      ""timestampMs"": ""{timestamp}"",
      ""latitudeE7"": {lat},
      ""longitudeE7"": {lon},
      ""accuracy"": {accuracy},
      ""activity"": [
        {{
          ""timestampMs"": ""{timestamp}"",
          ""activity"": [
            {{
              ""type"": ""{transportType}"",
              ""confidence"": {confidence}
            }}
          ]
        }}
      ]
    }}");

            if (i < locationPointCount - 1)
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

    // Model classes (reusing from UserJourneyTests with additional ones for statistics)
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

    private class DailyDistancesResponse
    {
        public List<DailyDistance> DailyDistances { get; set; } = new List<DailyDistance>();
        public int TotalDays { get; set; }
        public DateRange DateRange { get; set; } = new DateRange();
        public Pagination? Pagination { get; set; }
    }

    private class DailyDistance
    {
        public string Date { get; set; } = string.Empty;
        public double TotalDistanceKm { get; set; }
        public int MotorizedTrips { get; set; }
        public double LongestTripKm { get; set; }
        public double AverageSpeedKmh { get; set; }
        public List<string> TransportModes { get; set; } = new List<string>();
    }

    private class Pagination
    {
        public bool HasMore { get; set; }
        public string? NextCursor { get; set; }
    }

    private class DrivingStatistics
    {
        public int TotalDaysWithDriving { get; set; }
        public double TotalDistanceKm { get; set; }
        public double AverageDailyDistance { get; set; }
        public double MedianDailyDistance { get; set; }
        public double MaximumDailyDistance { get; set; }
        public double MinimumDailyDistance { get; set; }
        public double StandardDeviation { get; set; }
        public PercentileDistances PercentileDistances { get; set; } = new PercentileDistances();
        public TransportModeBreakdown TransportModeBreakdown { get; set; } = new TransportModeBreakdown();
    }

    private class PercentileDistances
    {
        public double P50 { get; set; }
        public double P75 { get; set; }
        public double P90 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
    }

    private class TransportModeBreakdown
    {
        public double InVehicle { get; set; }
        public double InBus { get; set; }
        public double OnMotorcycle { get; set; }
    }
}