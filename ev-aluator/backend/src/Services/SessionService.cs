using EVRangeAnalyzer.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EVRangeAnalyzer.Services;

/// <summary>
/// Service for managing user sessions and Google OAuth authentication flow.
/// Handles session creation, validation, renewal, and cleanup operations.
/// Integrates with Redis cache for session persistence and TTL management.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new user session from Google OAuth user profile data.
    /// </summary>
    /// <param name="googleUserId">Google user ID from OAuth token.</param>
    /// <param name="email">User email from Google profile.</param>
    /// <param name="name">User display name (optional).</param>
    /// <param name="pictureUrl">Profile picture URL (optional).</param>
    /// <param name="accessToken">OAuth access token for validation.</param>
    /// <returns>A new user session with generated session ID.</returns>
    Task<UserSession> CreateSessionAsync(string googleUserId, string email, string? name = null, string? pictureUrl = null, string? accessToken = null);

    /// <summary>
    /// Validates and retrieves a user session by session ID.
    /// </summary>
    /// <param name="sessionId">The session ID to validate.</param>
    /// <returns>The user session if valid, null if expired or not found.</returns>
    Task<UserSession?> GetSessionAsync(string sessionId);

    /// <summary>
    /// Updates session activity timestamp and extends TTL.
    /// </summary>
    /// <param name="sessionId">The session ID to update.</param>
    /// <returns>True if session was updated successfully.</returns>
    Task<bool> UpdateSessionActivityAsync(string sessionId);

    /// <summary>
    /// Changes session state for workflow tracking.
    /// </summary>
    /// <param name="sessionId">The session ID to update.</param>
    /// <param name="newState">The new session state.</param>
    /// <returns>True if state was updated successfully.</returns>
    Task<bool> ChangeSessionStateAsync(string sessionId, SessionState newState);

    /// <summary>
    /// Invalidates and removes a user session (logout).
    /// </summary>
    /// <param name="sessionId">The session ID to invalidate.</param>
    /// <returns>True if session was invalidated successfully.</returns>
    Task<bool> InvalidateSessionAsync(string sessionId);

    /// <summary>
    /// Validates Google OAuth access token and extracts user information.
    /// </summary>
    /// <param name="accessToken">The OAuth access token to validate.</param>
    /// <returns>User profile information if token is valid, null otherwise.</returns>
    Task<GoogleUserProfile?> ValidateGoogleTokenAsync(string accessToken);

    /// <summary>
    /// Finds active sessions for a given user ID (for cleanup and management).
    /// </summary>
    /// <param name="googleUserId">The Google user ID to search for.</param>
    /// <returns>List of active sessions for the user.</returns>
    Task<List<UserSession>> GetUserSessionsAsync(string googleUserId);

    /// <summary>
    /// Cleans up expired sessions from the cache.
    /// </summary>
    /// <returns>Number of sessions cleaned up.</returns>
    Task<int> CleanupExpiredSessionsAsync();
}

/// <summary>
/// Implementation of session management service with Redis caching and Google OAuth integration.
/// </summary>
public class SessionService : ISessionService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<SessionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Redis key prefix for session storage.
    /// </summary>
    private const string SessionKeyPrefix = "ev-analysis:session:";

    /// <summary>
    /// Redis key prefix for user-to-session mapping.
    /// </summary>
    private const string UserSessionsKeyPrefix = "ev-analysis:user-sessions:";

    /// <summary>
    /// Google OAuth token validation endpoint.
    /// </summary>
    private const string GoogleTokenInfoEndpoint = "https://www.googleapis.com/oauth2/v1/tokeninfo";

    public SessionService(
        ICacheService cacheService,
        ILogger<SessionService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public async Task<UserSession> CreateSessionAsync(
        string googleUserId,
        string email,
        string? name = null,
        string? pictureUrl = null,
        string? accessToken = null)
    {
        if (string.IsNullOrWhiteSpace(googleUserId))
            throw new ArgumentException("Google user ID cannot be null or empty", nameof(googleUserId));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty", nameof(email));

        try
        {
            // Create new session with Google profile data
            var session = UserSession.CreateFromGoogleProfile(
                googleUserId,
                email,
                name,
                pictureUrl,
                !string.IsNullOrWhiteSpace(accessToken) ? HashAccessToken(accessToken) : null);

            _logger.LogInformation("Creating new session {SessionId} for user {GoogleUserId}", 
                session.SessionId, googleUserId);

            // Store session in Redis with TTL
            var sessionKey = GetSessionKey(session.SessionId);
            var sessionJson = JsonSerializer.Serialize(session);
            
            await _cacheService.SetAsync(sessionKey, sessionJson, session.GetTimeUntilExpiration());

            // Maintain user-to-sessions mapping for cleanup
            await AddToUserSessionsAsync(googleUserId, session.SessionId);

            _logger.LogInformation("Successfully created session {SessionId} for user {GoogleUserId} with TTL {TTL}", 
                session.SessionId, googleUserId, session.GetTimeUntilExpiration());

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for user {GoogleUserId}", googleUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<UserSession?> GetSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        try
        {
            var sessionKey = GetSessionKey(sessionId);
            var sessionJson = await _cacheService.GetAsync(sessionKey);

            if (string.IsNullOrWhiteSpace(sessionJson))
            {
                _logger.LogDebug("Session {SessionId} not found in cache", sessionId);
                return null;
            }

            var session = JsonSerializer.Deserialize<UserSession>(sessionJson);
            if (session == null)
            {
                _logger.LogWarning("Failed to deserialize session {SessionId}", sessionId);
                return null;
            }

            // Validate session hasn't expired
            if (!session.IsValid())
            {
                _logger.LogDebug("Session {SessionId} is expired or invalid", sessionId);
                await InvalidateSessionAsync(sessionId);
                return null;
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve session {SessionId}", sessionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateSessionActivityAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
                return false;

            if (!session.UpdateActivity())
            {
                _logger.LogDebug("Failed to update activity for session {SessionId} - session may be expired", sessionId);
                return false;
            }

            // Update session in Redis with new TTL
            var sessionKey = GetSessionKey(sessionId);
            var sessionJson = JsonSerializer.Serialize(session);
            await _cacheService.SetAsync(sessionKey, sessionJson, session.GetTimeUntilExpiration());

            _logger.LogDebug("Updated activity for session {SessionId} with new TTL {TTL}", 
                sessionId, session.GetTimeUntilExpiration());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update session activity for {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ChangeSessionStateAsync(string sessionId, SessionState newState)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
                return false;

            var oldState = session.State;
            session.ChangeState(newState, updateActivity: true);

            // Update session in Redis
            var sessionKey = GetSessionKey(sessionId);
            var sessionJson = JsonSerializer.Serialize(session);
            await _cacheService.SetAsync(sessionKey, sessionJson, session.GetTimeUntilExpiration());

            _logger.LogDebug("Changed session {SessionId} state from {OldState} to {NewState}", 
                sessionId, oldState, newState);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change session state for {SessionId} to {NewState}", sessionId, newState);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> InvalidateSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
                return true; // Already gone

            // Remove from user sessions mapping
            await RemoveFromUserSessionsAsync(session.UserId, sessionId);

            // Remove from Redis
            var sessionKey = GetSessionKey(sessionId);
            await _cacheService.DeleteAsync(sessionKey);

            _logger.LogInformation("Invalidated session {SessionId} for user {UserId}", sessionId, session.UserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<GoogleUserProfile?> ValidateGoogleTokenAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        try
        {
            var tokenInfoUrl = $"{GoogleTokenInfoEndpoint}?access_token={accessToken}";
            var response = await _httpClient.GetAsync(tokenInfoUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token validation failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var tokenInfo = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(tokenInfo);
            var root = document.RootElement;

            // Verify token is for our application
            var audience = root.GetProperty("audience").GetString();
            var expectedClientId = _configuration["Authentication:Google:ClientId"];
            
            if (audience != expectedClientId)
            {
                _logger.LogWarning("Token audience {Audience} does not match expected client ID {ClientId}", audience, expectedClientId);
                return null;
            }

            return new GoogleUserProfile
            {
                UserId = root.GetProperty("user_id").GetString() ?? throw new InvalidOperationException("Missing user_id"),
                Email = root.GetProperty("email").GetString() ?? throw new InvalidOperationException("Missing email"),
                VerifiedEmail = root.TryGetProperty("verified_email", out var verified) && verified.GetBoolean(),
                Name = root.TryGetProperty("given_name", out var name) ? name.GetString() : null,
                PictureUrl = root.TryGetProperty("picture", out var picture) ? picture.GetString() : null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Google access token");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<UserSession>> GetUserSessionsAsync(string googleUserId)
    {
        if (string.IsNullOrWhiteSpace(googleUserId))
            return new List<UserSession>();

        try
        {
            var userSessionsKey = GetUserSessionsKey(googleUserId);
            var sessionIds = await _cacheService.GetSetMembersAsync(userSessionsKey);
            
            var sessions = new List<UserSession>();
            foreach (var sessionId in sessionIds)
            {
                var session = await GetSessionAsync(sessionId);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }

            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sessions for user {GoogleUserId}", googleUserId);
            return new List<UserSession>();
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredSessionsAsync()
    {
        // This will be called by a background service
        // For now, Redis TTL handles automatic cleanup
        // In the future, we could implement active cleanup here
        _logger.LogDebug("Session cleanup completed - using Redis TTL for automatic expiration");
        return 0;
    }

    /// <summary>
    /// Generates a Redis cache key for a session.
    /// </summary>
    private static string GetSessionKey(string sessionId) => $"{SessionKeyPrefix}{sessionId}";

    /// <summary>
    /// Generates a Redis cache key for user sessions mapping.
    /// </summary>
    private static string GetUserSessionsKey(string googleUserId) => $"{UserSessionsKeyPrefix}{googleUserId}";

    /// <summary>
    /// Hashes an OAuth access token for secure storage.
    /// </summary>
    private static string HashAccessToken(string accessToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(accessToken));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Adds a session ID to a user's session set.
    /// </summary>
    private async Task AddToUserSessionsAsync(string googleUserId, string sessionId)
    {
        var userSessionsKey = GetUserSessionsKey(googleUserId);
        await _cacheService.SetAddAsync(userSessionsKey, sessionId);
        // Set expiration on the user sessions key (longer than individual sessions)
        await _cacheService.ExpireAsync(userSessionsKey, TimeSpan.FromHours(3));
    }

    /// <summary>
    /// Removes a session ID from a user's session set.
    /// </summary>
    private async Task RemoveFromUserSessionsAsync(string googleUserId, string sessionId)
    {
        var userSessionsKey = GetUserSessionsKey(googleUserId);
        await _cacheService.SetRemoveAsync(userSessionsKey, sessionId);
    }
}

/// <summary>
/// Represents Google user profile information extracted from OAuth token validation.
/// </summary>
public class GoogleUserProfile
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool VerifiedEmail { get; set; }
    public string? Name { get; set; }
    public string? PictureUrl { get; set; }
}