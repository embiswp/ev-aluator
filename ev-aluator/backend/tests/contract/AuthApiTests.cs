namespace EVRangeAnalyzer.Tests.Contract;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Contract tests for authentication endpoints as specified in auth-api.yaml.
/// These tests verify the API contract compliance without requiring actual Google OAuth.
/// </summary>
public class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthApiTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    public AuthApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Tests GET /auth/login endpoint contract.
    /// Should redirect to Google OAuth with PKCE challenge.
    /// </summary>
    [Fact]
    public async Task GetAuthLogin_ShouldReturnRedirectToGoogleOAuth()
    {
        // Act
        var response = await _client.GetAsync("/auth/login");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("accounts.google.com", location);
        Assert.Contains("oauth2/auth", location);

        // Verify PKCE parameters are included
        Assert.Contains("code_challenge", location);
        Assert.Contains("code_challenge_method=S256", location);
        Assert.Contains("response_type=code", location);
    }

    /// <summary>
    /// Tests GET /auth/login with invalid redirect URI.
    /// Should return 400 Bad Request with error response.
    /// </summary>
    [Fact]
    public async Task GetAuthLogin_WithInvalidRedirectUri_ShouldReturn400()
    {
        // Act
        var response = await _client.GetAsync("/auth/login?redirect_uri=invalid-uri");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(errorResponse);
        Assert.Equal("INVALID_REDIRECT_URI", errorResponse.Error);
        Assert.Contains("redirect URI format", errorResponse.Message);
    }

    /// <summary>
    /// Tests GET /auth/callback endpoint contract with valid parameters.
    /// Should return 200 OK with auth response and set session cookie.
    /// </summary>
    [Fact]
    public async Task GetAuthCallback_WithValidParameters_ShouldReturn200WithAuthResponse()
    {
        // Arrange
        var code = "mock-authorization-code";
        var state = "mock-state-parameter";

        // Act
        var response = await _client.GetAsync($"/auth/callback?code={code}&state={state}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(authResponse);
        Assert.True(authResponse.Success);
        Assert.NotNull(authResponse.User);
        Assert.NotNull(authResponse.SessionId);

        // Verify session cookie is set
        var setCookieHeader = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.NotNull(setCookieHeader);
        Assert.Contains("ev-session", setCookieHeader);
        Assert.Contains("HttpOnly", setCookieHeader);
        Assert.Contains("Secure", setCookieHeader);
    }

    /// <summary>
    /// Tests GET /auth/callback with invalid authorization code.
    /// Should return 400 Bad Request with error response.
    /// </summary>
    [Fact]
    public async Task GetAuthCallback_WithInvalidCode_ShouldReturn400()
    {
        // Act
        var response = await _client.GetAsync("/auth/callback?code=invalid-code&state=valid-state");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(errorResponse);
        Assert.Equal("INVALID_AUTHORIZATION_CODE", errorResponse.Error);
    }

    /// <summary>
    /// Tests GET /auth/callback with missing state parameter.
    /// Should return 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task GetAuthCallback_WithMissingState_ShouldReturn400()
    {
        // Act
        var response = await _client.GetAsync("/auth/callback?code=valid-code");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests GET /auth/user endpoint contract with valid session.
    /// Should return 200 OK with user profile.
    /// </summary>
    [Fact]
    public async Task GetAuthUser_WithValidSession_ShouldReturn200WithUserProfile()
    {
        // Arrange - Set up authenticated session
        var sessionCookie = await CreateAuthenticatedSession();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        // Act
        var response = await _client.GetAsync("/auth/user");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var userProfile = JsonSerializer.Deserialize<UserProfile>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(userProfile);
        Assert.NotNull(userProfile.UserId);
        Assert.NotNull(userProfile.Email);
        Assert.NotNull(userProfile.Name);
        Assert.Contains("@", userProfile.Email);
    }

    /// <summary>
    /// Tests GET /auth/user endpoint without authentication.
    /// Should return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetAuthUser_WithoutAuthentication_ShouldReturn401()
    {
        // Act
        var response = await _client.GetAsync("/auth/user");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(errorResponse);
        Assert.Equal("AUTHENTICATION_REQUIRED", errorResponse.Error);
    }

    /// <summary>
    /// Tests POST /auth/logout endpoint contract.
    /// Should return 200 OK with logout response and clear session cookie.
    /// </summary>
    [Fact]
    public async Task PostAuthLogout_WithValidSession_ShouldReturn200AndClearCookie()
    {
        // Arrange - Set up authenticated session
        var sessionCookie = await CreateAuthenticatedSession();
        _client.DefaultRequestHeaders.Add("Cookie", $"ev-session={sessionCookie}");

        // Act
        var response = await _client.PostAsync("/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var logoutResponse = JsonSerializer.Deserialize<LogoutResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.NotNull(logoutResponse);
        Assert.True(logoutResponse.Success);
        Assert.Equal("Logged out successfully", logoutResponse.Message);

        // Verify session cookie is cleared
        var setCookieHeader = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.NotNull(setCookieHeader);
        Assert.Contains("ev-session=;", setCookieHeader);
        Assert.Contains("expires=", setCookieHeader);
    }

    /// <summary>
    /// Tests POST /auth/logout without authentication.
    /// Should return 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task PostAuthLogout_WithoutAuthentication_ShouldReturn401()
    {
        // Act
        var response = await _client.PostAsync("/auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Creates a mock authenticated session for testing purposes.
    /// This will need to be replaced with actual session creation once implemented.
    /// </summary>
    /// <returns>A session cookie value.</returns>
    private async Task<string> CreateAuthenticatedSession()
    {
        // This is a placeholder that will fail until actual auth implementation is created
        // In TDD, this failure is expected and drives the implementation
        await Task.CompletedTask;
        return "mock-session-cookie-value";
    }

    /// <summary>
    /// Contract model for error responses as defined in auth-api.yaml.
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
    /// Contract model for authentication response as defined in auth-api.yaml.
    /// </summary>
    private class AuthResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether authentication was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the user profile.
        /// </summary>
        public UserProfile User { get; set; } = new UserProfile();

        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contract model for user profile as defined in auth-api.yaml.
    /// </summary>
    private class UserProfile
    {
        /// <summary>
        /// Gets or sets the Google user identifier.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user profile picture URL.
        /// </summary>
        public string? Picture { get; set; }
    }

    /// <summary>
    /// Contract model for logout response as defined in auth-api.yaml.
    /// </summary>
    private class LogoutResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether logout was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the logout message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}