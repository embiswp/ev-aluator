using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EVRangeAnalyzer.Models;

/// <summary>
/// Represents an authenticated user session with temporary data storage.
/// Sessions are created during Google OAuth login and destroyed on logout or timeout.
/// Stored in Redis cache with automatic TTL expiration.
/// </summary>
public class UserSession
{
    /// <summary>
    /// Gets or sets the unique session identifier (GUID format).
    /// Used as the Redis cache key and for session validation.
    /// </summary>
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Google user ID from OAuth token.
    /// Immutable identifier from Google's user profile.
    /// </summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address from Google profile.
    /// Used for display purposes and user identification.
    /// </summary>
    [Required]
    [EmailAddress]
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's display name from Google profile.
    /// Optional - may be null if user hasn't set a name.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the user's profile picture URL from Google.
    /// Optional - used for UI personalization.
    /// </summary>
    public string? ProfilePictureUrl { get; set; }

    /// <summary>
    /// Gets or sets the session creation timestamp (UTC).
    /// Used for session age calculations and cleanup.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last activity timestamp (UTC).
    /// Updated on each API request for idle timeout calculations.
    /// </summary>
    [Required]
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the absolute session expiration time (UTC).
    /// Maximum session lifetime is 2 hours regardless of activity.
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the session is currently active.
    /// Set to false when user logs out or session is invalidated.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the current session state for processing workflow tracking.
    /// </summary>
    public SessionState State { get; set; } = SessionState.Active;

    /// <summary>
    /// Gets or sets OAuth access token hash for token validation.
    /// Not the actual token - a hash for security verification.
    /// </summary>
    [JsonIgnore] // Never serialize tokens to client
    public string? AccessTokenHash { get; set; }

    /// <summary>
    /// Gets or sets the Redis TTL expiration time for cache management.
    /// Automatically updated based on idle timeout policy.
    /// </summary>
    [JsonIgnore] // Internal cache management field
    public DateTime CacheTtl { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSession"/> class.
    /// Sets default expiration times based on security policy.
    /// </summary>
    public UserSession()
    {
        SessionId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        CreatedAt = now;
        LastAccessedAt = now;
        ExpiresAt = now.AddHours(2); // 2 hour absolute maximum
        CacheTtl = now.AddMinutes(30); // 30 minute idle timeout
    }

    /// <summary>
    /// Creates a new user session from Google OAuth user profile.
    /// </summary>
    /// <param name="googleUserId">The Google user ID from OAuth token.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="name">The user's display name (optional).</param>
    /// <param name="pictureUrl">The user's profile picture URL (optional).</param>
    /// <param name="accessTokenHash">Hash of the OAuth access token for validation.</param>
    /// <returns>A new UserSession configured with Google profile data.</returns>
    public static UserSession CreateFromGoogleProfile(
        string googleUserId,
        string email,
        string? name = null,
        string? pictureUrl = null,
        string? accessTokenHash = null)
    {
        if (string.IsNullOrWhiteSpace(googleUserId))
            throw new ArgumentException("Google user ID cannot be null or empty", nameof(googleUserId));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty", nameof(email));

        if (!IsValidEmail(email))
            throw new ArgumentException("Invalid email format", nameof(email));

        return new UserSession
        {
            UserId = googleUserId,
            UserEmail = email,
            UserName = name,
            ProfilePictureUrl = pictureUrl,
            AccessTokenHash = accessTokenHash,
            State = SessionState.Active,
        };
    }

    /// <summary>
    /// Updates the session's last accessed timestamp and extends TTL if needed.
    /// Called on each authenticated API request to maintain session activity.
    /// </summary>
    /// <returns>True if session was successfully updated, false if session is expired or inactive.</returns>
    public bool UpdateActivity()
    {
        if (!IsValid())
        {
            return false;
        }

        var now = DateTime.UtcNow;
        LastAccessedAt = now;

        // Extend TTL by 30 minutes from last activity (sliding expiration)
        // but never beyond the absolute expiration time
        var newTtl = now.AddMinutes(30);
        CacheTtl = newTtl < ExpiresAt ? newTtl : ExpiresAt;

        return true;
    }

    /// <summary>
    /// Validates the current session state and expiration times.
    /// </summary>
    /// <returns>True if the session is valid and active, false otherwise.</returns>
    public bool IsValid()
    {
        var now = DateTime.UtcNow;

        // Check if session is marked as inactive
        if (!IsActive)
        {
            return false;
        }

        // Check if session has expired (either absolute or idle timeout)
        if (now >= ExpiresAt || now >= CacheTtl)
        {
            return false;
        }

        // Check if session ID is valid
        if (string.IsNullOrWhiteSpace(SessionId) || !IsValidGuid(SessionId))
        {
            return false;
        }

        // Check if user information is present
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(UserEmail))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the session has expired based on current time.
    /// </summary>
    /// <returns>True if the session is expired, false otherwise.</returns>
    public bool IsExpired()
    {
        var now = DateTime.UtcNow;
        return now >= ExpiresAt || now >= CacheTtl || !IsActive;
    }

    /// <summary>
    /// Marks the session as expired and inactive.
    /// Should be called during logout or when session validation fails.
    /// </summary>
    public void Invalidate()
    {
        IsActive = false;
        State = SessionState.Expired;
        
        // Set expiration to past time to ensure cache cleanup
        var past = DateTime.UtcNow.AddMinutes(-1);
        ExpiresAt = past;
        CacheTtl = past;
    }

    /// <summary>
    /// Gets the remaining time until session expiration.
    /// </summary>
    /// <returns>TimeSpan until expiration, or TimeSpan.Zero if already expired.</returns>
    public TimeSpan GetTimeUntilExpiration()
    {
        if (!IsValid())
        {
            return TimeSpan.Zero;
        }

        var now = DateTime.UtcNow;
        var absoluteRemaining = ExpiresAt - now;
        var idleRemaining = CacheTtl - now;

        // Return the shorter of the two timeouts
        var remaining = absoluteRemaining < idleRemaining ? absoluteRemaining : idleRemaining;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Changes the session state for workflow tracking.
    /// </summary>
    /// <param name="newState">The new session state.</param>
    /// <param name="updateActivity">Whether to update the last accessed timestamp.</param>
    public void ChangeState(SessionState newState, bool updateActivity = true)
    {
        State = newState;

        if (updateActivity)
        {
            UpdateActivity();
        }
    }

    /// <summary>
    /// Creates a sanitized copy of the session for client serialization.
    /// Removes sensitive fields like access token hashes and internal cache data.
    /// </summary>
    /// <returns>A UserSession suitable for sending to the client.</returns>
    public UserSession ToClientSession()
    {
        return new UserSession
        {
            SessionId = SessionId,
            UserId = UserId,
            UserEmail = UserEmail,
            UserName = UserName,
            ProfilePictureUrl = ProfilePictureUrl,
            CreatedAt = CreatedAt,
            LastAccessedAt = LastAccessedAt,
            ExpiresAt = ExpiresAt,
            IsActive = IsActive,
            State = State,
            // Explicitly exclude sensitive fields
            AccessTokenHash = null,
            CacheTtl = default,
        };
    }

    /// <summary>
    /// Validates email format using basic regex pattern.
    /// </summary>
    /// <param name="email">Email address to validate.</param>
    /// <returns>True if email format is valid.</returns>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates GUID format for session IDs.
    /// </summary>
    /// <param name="sessionId">Session ID to validate.</param>
    /// <returns>True if session ID is a valid GUID.</returns>
    private static bool IsValidGuid(string sessionId)
    {
        return Guid.TryParse(sessionId, out _);
    }
}

/// <summary>
/// Represents the current state of a user session for workflow tracking.
/// </summary>
public enum SessionState
{
    /// <summary>
    /// Session is active and ready for operations.
    /// </summary>
    Active,

    /// <summary>
    /// Session is processing a file upload or analysis operation.
    /// </summary>
    Processing,

    /// <summary>
    /// Session has expired due to timeout or manual logout.
    /// </summary>
    Expired,

    /// <summary>
    /// Session encountered an error and may need cleanup.
    /// </summary>
    Error,
}