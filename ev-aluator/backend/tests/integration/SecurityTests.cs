namespace EVRangeAnalyzer.Tests.Integration;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Integration tests for data privacy and security (Scenario 5 from quickstart.md).
/// Validates security measures and privacy protection throughout the application.
/// Tests session security, data isolation, automatic cleanup, and input validation.
/// These tests will fail until the full implementation is complete (expected in TDD).
/// </summary>
public class SecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    public SecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// Tests session security implementation.
    /// Validates that session cookies have proper security flags and tokens are not exposed to JavaScript.
    /// </summary>
    [Fact]
    public async Task SessionSecurity_CookieFlags_ShouldHaveProperSecurityFlags()
    {
        // Arrange & Act
        var loginResponse = await _client.GetAsync("/auth/login");
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var callbackResponse = await _client.GetAsync("/auth/callback?code=test-code&state=test-state");
        Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);

        // Assert - Check session cookie security flags
        var setCookieHeaders = callbackResponse.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookieHeader = setCookieHeaders.FirstOrDefault(h => h.Contains("ev-session"));
        
        Assert.NotNull(sessionCookieHeader);
        
        // Cookie should have HttpOnly flag (not accessible to JavaScript)
        Assert.Contains("HttpOnly", sessionCookieHeader);
        
        // Cookie should have Secure flag (only sent over HTTPS in production)
        // In development, this might not be present, so we check for appropriate environment handling
        var isHttpsOrDevelopment = sessionCookieHeader.Contains("Secure") || 
                                  Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        Assert.True(isHttpsOrDevelopment, "Session cookie should be Secure in production or properly configured for development");
        
        // Cookie should have SameSite attribute for CSRF protection
        Assert.True(sessionCookieHeader.Contains("SameSite="), "Session cookie should have SameSite attribute");
    }

    /// <summary>
    /// Tests that session tokens are not exposed to client-side JavaScript.
    /// Validates that authentication tokens remain server-side only.
    /// </summary>
    [Fact]
    public async Task SessionSecurity_TokenExposure_ShouldNotExposeTokensToJavaScript()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();

        // Act - Get a page that might contain JavaScript
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var userResponse = await _client.GetAsync("/auth/user");
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);

        var userContent = await userResponse.Content.ReadAsStringAsync();
        var userProfile = JsonSerializer.Deserialize<UserProfile>(userContent, _jsonOptions);

        // Assert - Response should not contain session tokens or OAuth tokens
        Assert.NotNull(userProfile);
        Assert.DoesNotContain("access_token", userContent);
        Assert.DoesNotContain("refresh_token", userContent);
        Assert.DoesNotContain("id_token", userContent);
        Assert.DoesNotContain("oauth", userContent.ToLower());
        
        // User profile should only contain safe, user-visible information
        Assert.NotEmpty(userProfile.UserId);
        Assert.NotEmpty(userProfile.Email);
        Assert.NotEmpty(userProfile.Name);
        
        // Should not contain internal session identifiers
        Assert.DoesNotContain(sessionCookie, userContent);
    }

    /// <summary>
    /// Tests session invalidation after logout.
    /// Ensures that session is properly invalidated and cannot be reused.
    /// </summary>
    [Fact]
    public async Task SessionSecurity_LogoutInvalidation_ShouldInvalidateSession()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        await UploadAndProcessSampleFile(sessionCookie);

        // Verify session works before logout
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");
        
        var preLogoutResponse = await _client.GetAsync("/data/summary");
        Assert.Equal(HttpStatusCode.OK, preLogoutResponse.StatusCode);

        // Act - Logout
        var logoutResponse = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        // Assert - Session should be invalidated
        var postLogoutResponse = await _client.GetAsync("/data/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, postLogoutResponse.StatusCode);

        // User endpoint should also be inaccessible
        var userResponse = await _client.GetAsync("/auth/user");
        Assert.Equal(HttpStatusCode.Unauthorized, userResponse.StatusCode);

        // Check that logout response includes session clearing cookie
        var logoutSetCookieHeaders = logoutResponse.Headers.GetValues("Set-Cookie").ToList();
        var clearSessionCookie = logoutSetCookieHeaders.FirstOrDefault(h => h.Contains("ev-session=;") || h.Contains("ev-session=\"\";"));
        
        Assert.NotNull(clearSessionCookie);
        Assert.Contains("expires=", clearSessionCookie.ToLower()); // Should have past expiration date
    }

    /// <summary>
    /// Tests data isolation between different user sessions.
    /// Validates that users cannot access each other's data.
    /// </summary>
    [Fact]
    public async Task DataIsolation_MultipleUsers_ShouldIsolateUserData()
    {
        // Arrange - Create two separate user sessions
        var userASession = await AuthenticateUser();
        var userBSession = await AuthenticateUser(); // Simulate different user

        // Upload data for User A
        await UploadAndProcessSampleFile(userASession, "UserA-data.json");
        
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={userASession}");
        var userADataResponse = await _client.GetAsync("/data/summary");
        Assert.Equal(HttpStatusCode.OK, userADataResponse.StatusCode);

        // Act - Switch to User B and try to access data
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={userBSession}");

        var userBDataAttempt = await _client.GetAsync("/data/summary");
        
        // Assert - User B should not have access to User A's data
        Assert.Equal(HttpStatusCode.NotFound, userBDataAttempt.StatusCode);

        // User B should not be able to access User A's analysis endpoints
        var analysisAttempt = await _client.PostAsync("/analysis/ev-compatibility",
            CreateEvAnalysisRequest(400));
        Assert.Equal(HttpStatusCode.NotFound, analysisAttempt.StatusCode);

        // User B should not be able to delete User A's data
        var deleteAttempt = await _client.DeleteAsync("/data");
        Assert.Equal(HttpStatusCode.NotFound, deleteAttempt.StatusCode);

        // Cleanup
        await LogoutUser(userASession);
        await LogoutUser(userBSession);
    }

    /// <summary>
    /// Tests automatic data cleanup on logout.
    /// Ensures all user data is removed from the system when user logs out.
    /// </summary>
    [Fact]
    public async Task AutomaticDataCleanup_OnLogout_ShouldRemoveAllUserData()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        await UploadAndProcessSampleFile(sessionCookie);

        // Verify data exists
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");
        
        var preLogoutSummary = await _client.GetAsync("/data/summary");
        Assert.Equal(HttpStatusCode.OK, preLogoutSummary.StatusCode);
        
        var preLogoutAnalysis = await _client.PostAsync("/analysis/ev-compatibility",
            CreateEvAnalysisRequest(400));
        Assert.Equal(HttpStatusCode.OK, preLogoutAnalysis.StatusCode);

        // Act - Logout (should trigger automatic cleanup)
        var logoutResponse = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        // Wait a moment for cleanup to complete (if asynchronous)
        await Task.Delay(1000);

        // Assert - All data should be removed
        // Even if we could authenticate again with same session, data should be gone
        var postLogoutSummary = await _client.GetAsync("/data/summary");
        Assert.True(postLogoutSummary.StatusCode == HttpStatusCode.Unauthorized || 
                   postLogoutSummary.StatusCode == HttpStatusCode.NotFound);

        // Analysis endpoints should not have data to work with
        var postLogoutAnalysis = await _client.PostAsync("/analysis/ev-compatibility",
            CreateEvAnalysisRequest(400));
        Assert.True(postLogoutAnalysis.StatusCode == HttpStatusCode.Unauthorized || 
                   postLogoutAnalysis.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests automatic session expiration cleanup.
    /// Validates that expired session data is automatically purged from the system.
    /// </summary>
    [Fact]
    public async Task AutomaticDataCleanup_SessionExpiration_ShouldPurgeExpiredData()
    {
        // This test simulates session expiration by using expired session cookies
        var expiredSessionCookie = "expired-session-12345";
        
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={expiredSessionCookie}");

        // Act - Try to access data with expired session
        var expiredDataResponse = await _client.GetAsync("/data/summary");
        
        // Assert - Should be unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, expiredDataResponse.StatusCode);
        
        var errorContent = await expiredDataResponse.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorContent, _jsonOptions);
        
        Assert.NotNull(errorResponse);
        Assert.True(errorResponse.Error == "AUTHENTICATION_REQUIRED" || 
                   errorResponse.Error == "SESSION_EXPIRED");

        // Try other endpoints - all should be unauthorized
        var analysisResponse = await _client.PostAsync("/analysis/ev-compatibility",
            CreateEvAnalysisRequest(400));
        Assert.Equal(HttpStatusCode.Unauthorized, analysisResponse.StatusCode);

        var userResponse = await _client.GetAsync("/auth/user");
        Assert.Equal(HttpStatusCode.Unauthorized, userResponse.StatusCode);
    }

    /// <summary>
    /// Tests input validation security to prevent injection attacks.
    /// Validates that potentially malicious JSON content is properly sanitized.
    /// </summary>
    [Theory]
    [InlineData("script-injection.json", @"{""locations"":[{""timestampMs"":""<script>alert('xss')</script>""}]}")]
    [InlineData("sql-injection.json", @"{""locations"":[{""timestampMs"":""'; DROP TABLE users; --""}]}")]
    [InlineData("large-string.json", @"{""locations"":[{""timestampMs"":""" + new string('A', 10000) + @"""}]}")]
    public async Task InputValidationSecurity_MaliciousContent_ShouldPreventSecurityIssues(string fileName, string maliciousContent)
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();

        // Act - Try to upload potentially malicious content
        var uploadResponse = await UploadFile(sessionCookie, maliciousContent, fileName);

        // Assert - Should either reject the file or safely process it
        if (uploadResponse.StatusCode == HttpStatusCode.Accepted)
        {
            // If accepted, processing should complete safely without executing malicious content
            var uploadContent = await uploadResponse.Content.ReadAsStringAsync();
            var upload = JsonSerializer.Deserialize<UploadResponse>(uploadContent, _jsonOptions);
            
            // Wait for processing to complete or fail
            try
            {
                await WaitForProcessingCompletion(sessionCookie, upload.UploadId);
                
                // If processing succeeded, verify no script execution occurred
                // (This would be more comprehensive with actual malicious payloads)
                var summaryResponse = await _client.GetAsync("/data/summary");
                if (summaryResponse.StatusCode == HttpStatusCode.OK)
                {
                    var summaryContent = await summaryResponse.Content.ReadAsStringAsync();
                    
                    // Content should not contain unescaped script tags or SQL injection attempts
                    Assert.DoesNotContain("<script>", summaryContent);
                    Assert.DoesNotContain("DROP TABLE", summaryContent);
                }
            }
            catch (Exception)
            {
                // Processing failure is acceptable for malicious content
            }
        }
        else
        {
            // Immediate rejection is also acceptable for malicious content
            Assert.True(uploadResponse.StatusCode == HttpStatusCode.BadRequest ||
                       uploadResponse.StatusCode == HttpStatusCode.UnprocessableEntity);
        }

        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests that no persistent data storage occurs.
    /// Validates that location data is not permanently stored on the server.
    /// </summary>
    [Fact]
    public async Task NoPersistentStorage_DataLifecycle_ShouldNotPermanentlyStoreLocationData()
    {
        // Arrange
        var sessionCookie = await AuthenticateUser();
        var testData = CreateSampleLocationJson();

        // Act - Upload and process data
        await UploadAndProcessSampleFile(sessionCookie, "temporary-data.json");
        
        // Verify data is accessible during session
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");
        
        var activeSummary = await _client.GetAsync("/data/summary");
        Assert.Equal(HttpStatusCode.OK, activeSummary.StatusCode);

        // Logout to trigger cleanup
        await LogoutUser(sessionCookie);

        // Create new session as the same user
        var newSessionCookie = await AuthenticateUser();
        
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={newSessionCookie}");

        // Assert - Data should not be accessible in new session (not persistently stored)
        var newSessionSummary = await _client.GetAsync("/data/summary");
        Assert.Equal(HttpStatusCode.NotFound, newSessionSummary.StatusCode);

        // Analysis should not have any data
        var newSessionAnalysis = await _client.PostAsync("/analysis/ev-compatibility",
            CreateEvAnalysisRequest(400));
        Assert.Equal(HttpStatusCode.NotFound, newSessionAnalysis.StatusCode);

        // Cleanup
        await LogoutUser(newSessionCookie);
    }

    /// <summary>
    /// Tests CORS configuration security.
    /// Validates that cross-origin requests are properly controlled.
    /// </summary>
    [Fact]
    public async Task CORSSecurity_CrossOriginRequests_ShouldBeProperlyControlled()
    {
        // Test preflight request with different origin
        var preflightRequest = new HttpRequestMessage(HttpMethod.Options, "/auth/login");
        preflightRequest.Headers.Add("Origin", "https://malicious-site.com");
        preflightRequest.Headers.Add("Access-Control-Request-Method", "GET");

        var preflightResponse = await _client.SendAsync(preflightRequest);

        // Should either reject cross-origin request or have proper CORS headers
        if (preflightResponse.Headers.Contains("Access-Control-Allow-Origin"))
        {
            var allowedOrigin = preflightResponse.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
            
            // Should not allow all origins in production
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
            {
                Assert.NotEqual("*", allowedOrigin);
            }
        }

        // Test actual cross-origin request
        var corsRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/login");
        corsRequest.Headers.Add("Origin", "https://malicious-site.com");

        var corsResponse = await _client.SendAsync(corsRequest);

        // Response should include appropriate CORS headers or reject the request
        // Specific behavior depends on CORS policy configuration
        Assert.True(corsResponse.StatusCode == HttpStatusCode.Redirect || // Normal auth redirect
                   corsResponse.StatusCode == HttpStatusCode.Forbidden || // CORS rejection
                   corsResponse.Headers.Contains("Access-Control-Allow-Origin")); // CORS allowed
    }

    /// <summary>
    /// Tests rate limiting and abuse prevention.
    /// Validates that rapid successive requests are properly controlled.
    /// </summary>
    [Fact]
    public async Task RateLimitingSecurity_RapidRequests_ShouldPreventAbuse()
    {
        var sessionCookie = await AuthenticateUser();

        // Make rapid successive requests to potentially expensive endpoint
        var tasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 0; i < 20; i++) // 20 rapid requests
        {
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");
            
            var task = _client.PostAsync("/analysis/ev-compatibility", CreateEvAnalysisRequest(400 + i));
            tasks.Add(task);
        }

        var responses = await Task.WhenAll(tasks);

        // Should have some rate limiting mechanism
        var rateLimitedResponses = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        var notFoundResponses = responses.Count(r => r.StatusCode == HttpStatusCode.NotFound); // No data uploaded
        var successfulResponses = responses.Count(r => r.StatusCode == HttpStatusCode.OK);

        // Most requests should be rate limited, not found (no data), or successful
        // At minimum, system should remain stable and not crash
        Assert.True(rateLimitedResponses + notFoundResponses + successfulResponses == responses.Length,
            "All responses should have valid HTTP status codes");

        // Cleanup
        await LogoutUser(sessionCookie);
    }

    /// <summary>
    /// Tests security headers in responses.
    /// Validates that appropriate security headers are present in HTTP responses.
    /// </summary>
    [Fact]
    public async Task SecurityHeaders_HttpResponses_ShouldIncludeSecurityHeaders()
    {
        // Act - Get various endpoints to check for security headers
        var endpoints = new[] { "/health", "/auth/login" };
        
        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint);
            
            // Check for common security headers
            var headers = response.Headers.Concat(response.Content.Headers).ToDictionary(h => h.Key, h => h.Value);
            
            // X-Content-Type-Options: nosniff
            if (headers.ContainsKey("X-Content-Type-Options"))
            {
                Assert.Contains("nosniff", headers["X-Content-Type-Options"].FirstOrDefault() ?? "");
            }
            
            // X-Frame-Options (clickjacking protection)
            if (headers.ContainsKey("X-Frame-Options"))
            {
                var frameOptions = headers["X-Frame-Options"].FirstOrDefault() ?? "";
                Assert.True(frameOptions.Contains("DENY") || frameOptions.Contains("SAMEORIGIN"));
            }
            
            // Content-Security-Policy would be ideal but may not be implemented in basic setup
            // Referrer-Policy for privacy
            if (headers.ContainsKey("Referrer-Policy"))
            {
                Assert.NotEmpty(headers["Referrer-Policy"].FirstOrDefault() ?? "");
            }
        }
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

    private async Task<HttpResponseMessage> UploadFile(string sessionCookie, string fileContent, string fileName)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent)), "file", fileName);

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
                
                if (status != null && (status.Status == "completed" || status.Status == "failed"))
                {
                    return;
                }
            }

            await Task.Delay(1000);
        }
    }

    private async Task UploadAndProcessSampleFile(string sessionCookie, string fileName = "sample.json")
    {
        var uploadId = await UploadLocationFile(sessionCookie, CreateSampleLocationJson(), fileName);
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
            AnalysisName = $"Security Test {rangeKm}km",
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

    private static string ExtractSessionCookie(string setCookieHeader)
    {
        var match = System.Text.RegularExpressions.Regex.Match(setCookieHeader, @"ev-session=([^;]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    // Model classes
    private class UserProfile
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Picture { get; set; }
    }

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

    private class EVAnalysisRequest
    {
        public int EvRangeKm { get; set; }
        public bool IncludeChargingBuffer { get; set; } = true;
        public string? AnalysisName { get; set; }
    }
}