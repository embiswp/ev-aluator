namespace EVRangeAnalyzer.Tests.Integration;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Integration tests for complete user journey (Scenario 1 from quickstart.md).
/// Tests the end-to-end workflow from authentication to analysis to logout.
/// These tests will fail until the full implementation is complete (expected in TDD).
/// </summary>
public class UserJourneyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserJourneyTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    public UserJourneyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// Tests the complete user journey from authentication to logout.
    /// Validates end-to-end functionality with typical user workflow.
    /// Expected to complete under 5 minutes with no errors.
    /// </summary>
    [Fact]
    public async Task CompleteUserJourney_HappyPath_ShouldCompleteSuccessfully()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Step 1: Navigate to application (simulated via API health check)
            await VerifyApplicationIsRunning();

            // Step 2: Authenticate with Google OAuth
            var sessionCookie = await AuthenticateWithGoogleOAuth();

            // Step 3: Upload location history file
            var uploadId = await UploadLocationHistoryFile(sessionCookie);

            // Step 4: Wait for processing and view data summary
            await WaitForProcessingCompletion(sessionCookie, uploadId);
            var dataSummary = await GetDataSummary(sessionCookie);

            // Step 5: Analyze EV compatibility with 400km range
            var analysis400km = await AnalyzeEVCompatibility(sessionCookie, 400);

            // Step 6: Review detailed results
            var dailyDistances = await GetDailyDistanceBreakdown(sessionCookie);

            // Step 7: Try different EV ranges (200km, 600km)
            var analysis200km = await AnalyzeEVCompatibility(sessionCookie, 200);
            var analysis600km = await AnalyzeEVCompatibility(sessionCookie, 600);

            // Step 8: Delete data and logout
            await DeleteUserData(sessionCookie);
            await LogoutUser(sessionCookie);

            // Verify acceptance criteria
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalMinutes < 5, 
                $"Complete workflow took {stopwatch.Elapsed.TotalMinutes:F2} minutes, should be under 5 minutes");

            // Verify data integrity
            VerifyDataSummaryIntegrity(dataSummary);
            VerifyAnalysisResults(analysis400km, analysis200km, analysis600km);
            VerifyDailyDistancesIntegrity(dailyDistances);
        }
        catch (Exception ex)
        {
            Assert.True(false, $"User journey failed with error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests performance requirements during user journey.
    /// Ensures analysis completes within 2 seconds and no performance degradation occurs.
    /// </summary>
    [Fact]
    public async Task CompleteUserJourney_PerformanceValidation_ShouldMeetTimingRequirements()
    {
        // Arrange
        var sessionCookie = await AuthenticateWithGoogleOAuth();
        await UploadAndProcessSampleFile(sessionCookie);

        // Act & Assert - Multiple EV analyses should each complete under 2 seconds
        var ranges = new[] { 200, 300, 400, 500, 600 };
        
        foreach (var range in ranges)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            await AnalyzeEVCompatibility(sessionCookie, range);
            
            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed.TotalSeconds < 2,
                $"EV analysis for {range}km took {stopwatch.Elapsed.TotalSeconds:F2} seconds, should be under 2 seconds");
        }

        // Cleanup
        await DeleteUserData(sessionCookie);
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests UI responsiveness and accessibility during user journey.
    /// Validates that all operations complete without blocking the user interface.
    /// </summary>
    [Fact]
    public async Task CompleteUserJourney_UIResponsiveness_ShouldMaintainResponsiveness()
    {
        // This test simulates concurrent operations to ensure API remains responsive
        var sessionCookie = await AuthenticateWithGoogleOAuth();
        await UploadAndProcessSampleFile(sessionCookie);

        // Simulate concurrent API calls that a responsive UI might make
        var tasks = new List<Task>();
        
        // Multiple concurrent analysis requests
        for (int i = 0; i < 5; i++)
        {
            var range = 200 + (i * 100); // 200, 300, 400, 500, 600
            tasks.Add(AnalyzeEVCompatibility(sessionCookie, range));
        }

        // Data summary requests
        tasks.Add(GetDataSummary(sessionCookie));
        tasks.Add(GetDailyDistanceBreakdown(sessionCookie));

        // Execute all requests concurrently
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // All concurrent operations should complete within reasonable time
        Assert.True(stopwatch.Elapsed.TotalSeconds < 10,
            $"Concurrent operations took {stopwatch.Elapsed.TotalSeconds:F2} seconds, should be under 10 seconds");

        // Cleanup
        await DeleteUserData(sessionCookie);
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests automatic data cleanup on logout.
    /// Ensures data is automatically cleared and session is properly terminated.
    /// </summary>
    [Fact]
    public async Task CompleteUserJourney_DataCleanup_ShouldClearAllUserData()
    {
        // Arrange
        var sessionCookie = await AuthenticateWithGoogleOAuth();
        await UploadAndProcessSampleFile(sessionCookie);

        // Verify data exists
        var dataSummary = await GetDataSummary(sessionCookie);
        Assert.NotNull(dataSummary);

        // Act - Logout (should trigger automatic cleanup)
        await LogoutUser(sessionCookie);

        // Assert - Data should no longer be accessible
        var response = await _client.GetAsync("/data/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Session should be invalidated
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");
        var authResponse = await _client.GetAsync("/auth/user");
        Assert.Equal(HttpStatusCode.Unauthorized, authResponse.StatusCode);
    }

    // Helper methods for user journey steps

    private async Task VerifyApplicationIsRunning()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> AuthenticateWithGoogleOAuth()
    {
        // Step 2.1: Initiate OAuth flow
        var loginResponse = await _client.GetAsync("/auth/login");
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // Step 2.2: Simulate OAuth callback (will fail until OAuth implementation is complete)
        var callbackResponse = await _client.GetAsync("/auth/callback?code=test-code&state=test-state");
        Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);

        // Extract session cookie
        var setCookieHeader = callbackResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.NotNull(setCookieHeader);
        
        var sessionCookie = ExtractSessionCookie(setCookieHeader);
        Assert.NotNull(sessionCookie);

        return sessionCookie;
    }

    private async Task<string> UploadLocationHistoryFile(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var fileContent = CreateSampleGoogleTakeoutJson();
        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent)), "file", "Location History.json");

        var response = await _client.PostAsync("/upload/location-history", formData);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

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
            }

            await Task.Delay(1000); // Wait 1 second before checking again
        }

        Assert.True(false, "Processing did not complete within 30 seconds");
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
            AnalysisName = $"Test Analysis {rangeKm}km",
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _client.PostAsync("/analysis/ev-compatibility", jsonContent);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.Elapsed.TotalSeconds < 2, 
            $"Analysis for {rangeKm}km took {stopwatch.Elapsed.TotalSeconds:F2} seconds, should be under 2 seconds");

        var content = await response.Content.ReadAsStringAsync();
        var analysis = JsonSerializer.Deserialize<EVAnalysisResponse>(content, _jsonOptions);
        
        Assert.NotNull(analysis);
        return analysis;
    }

    private async Task<DailyDistancesResponse> GetDailyDistanceBreakdown(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.GetAsync("/analysis/daily-distances?limit=100&sortBy=distance&sortOrder=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var dailyDistances = JsonSerializer.Deserialize<DailyDistancesResponse>(content, _jsonOptions);
        
        Assert.NotNull(dailyDistances);
        return dailyDistances;
    }

    private async Task DeleteUserData(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.DeleteAsync("/data");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var deleteResponse = JsonSerializer.Deserialize<DeleteResponse>(content, _jsonOptions);
        
        Assert.NotNull(deleteResponse);
        Assert.True(deleteResponse.Success);
    }

    private async Task LogoutUser(string sessionCookie)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var response = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var logoutResponse = JsonSerializer.Deserialize<LogoutResponse>(content, _jsonOptions);
        
        Assert.NotNull(logoutResponse);
        Assert.True(logoutResponse.Success);
    }

    private async Task UploadAndProcessSampleFile(string sessionCookie)
    {
        var uploadId = await UploadLocationHistoryFile(sessionCookie);
        await WaitForProcessingCompletion(sessionCookie, uploadId);
    }

    private static string CreateSampleGoogleTakeoutJson()
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

    private static string ExtractSessionCookie(string setCookieHeader)
    {
        // Extract session value from Set-Cookie header
        var match = System.Text.RegularExpressions.Regex.Match(setCookieHeader, @"ev-session=([^;]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void VerifyDataSummaryIntegrity(DataSummary summary)
    {
        Assert.True(summary.FileSize > 0, "File size should be greater than 0");
        Assert.True(summary.TotalLocationPoints > 0, "Should have location points");
        Assert.True(summary.TotalDays > 0, "Should have days with data");
        Assert.NotNull(summary.FileName);
        Assert.NotNull(summary.DateRange);
    }

    private static void VerifyAnalysisResults(EVAnalysisResponse analysis400km, EVAnalysisResponse analysis200km, EVAnalysisResponse analysis600km)
    {
        // 200km range should have lower compatibility than 400km
        // 600km range should have higher compatibility than 400km
        Assert.True(analysis200km.CompatibilityPercentage <= analysis400km.CompatibilityPercentage,
            "200km range should have lower or equal compatibility than 400km");
        
        Assert.True(analysis600km.CompatibilityPercentage >= analysis400km.CompatibilityPercentage,
            "600km range should have higher or equal compatibility than 400km");

        // All analyses should have valid data
        foreach (var analysis in new[] { analysis200km, analysis400km, analysis600km })
        {
            Assert.True(analysis.TotalDaysAnalyzed > 0);
            Assert.True(analysis.CompatibleDays >= 0);
            Assert.True(analysis.IncompatibleDays >= 0);
            Assert.True(analysis.CompatibilityPercentage >= 0 && analysis.CompatibilityPercentage <= 100);
        }
    }

    private static void VerifyDailyDistancesIntegrity(DailyDistancesResponse dailyDistances)
    {
        Assert.NotNull(dailyDistances.DailyDistances);
        Assert.True(dailyDistances.TotalDays > 0);
        Assert.NotNull(dailyDistances.DateRange);

        // Verify sorting (should be descending by distance)
        for (int i = 0; i < dailyDistances.DailyDistances.Count - 1; i++)
        {
            Assert.True(dailyDistances.DailyDistances[i].TotalDistanceKm >= 
                       dailyDistances.DailyDistances[i + 1].TotalDistanceKm,
                       "Daily distances should be sorted in descending order");
        }
    }

    // Model classes matching contract specifications
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

    private class DeleteResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DeletedItems DeletedItems { get; set; } = new DeletedItems();
    }

    private class DeletedItems
    {
        public int LocationPoints { get; set; }
        public int DailySummaries { get; set; }
    }

    private class LogoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}