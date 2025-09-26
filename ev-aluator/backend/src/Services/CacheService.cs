using StackExchange.Redis;
using System.Text.Json;

namespace EVRangeAnalyzer.Services;

/// <summary>
/// Service for Redis-based caching operations supporting session management and data storage.
/// Provides high-performance caching with TTL support, atomic operations, and connection management.
/// Optimized for session storage, temporary data, and performance-critical operations.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <param name="key">Cache key to retrieve.</param>
    /// <returns>Cached value as string, or null if not found or expired.</returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Sets a cached value with optional TTL.
    /// </summary>
    /// <param name="key">Cache key to set.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="expiry">Optional expiration time.</param>
    /// <returns>True if the value was set successfully.</returns>
    Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// Gets a cached object deserialized from JSON.
    /// </summary>
    /// <typeparam name="T">Type to deserialize to.</typeparam>
    /// <param name="key">Cache key to retrieve.</param>
    /// <returns>Deserialized object, or default(T) if not found.</returns>
    Task<T?> GetObjectAsync<T>(string key) where T : class;

    /// <summary>
    /// Sets a cached object serialized as JSON with optional TTL.
    /// </summary>
    /// <typeparam name="T">Type to serialize.</typeparam>
    /// <param name="key">Cache key to set.</param>
    /// <param name="value">Object to cache.</param>
    /// <param name="expiry">Optional expiration time.</param>
    /// <returns>True if the object was cached successfully.</returns>
    Task<bool> SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;

    /// <summary>
    /// Deletes a cached value by key.
    /// </summary>
    /// <param name="key">Cache key to delete.</param>
    /// <returns>True if the key was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    /// <param name="key">Cache key to check.</param>
    /// <returns>True if the key exists.</returns>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Sets expiration time for an existing key.
    /// </summary>
    /// <param name="key">Cache key to expire.</param>
    /// <param name="expiry">Expiration time.</param>
    /// <returns>True if expiration was set successfully.</returns>
    Task<bool> ExpireAsync(string key, TimeSpan expiry);

    /// <summary>
    /// Gets the remaining time to live for a key.
    /// </summary>
    /// <param name="key">Cache key to check.</param>
    /// <returns>Remaining TTL, or null if key doesn't exist or has no expiration.</returns>
    Task<TimeSpan?> GetTtlAsync(string key);

    /// <summary>
    /// Adds a value to a Redis set.
    /// </summary>
    /// <param name="key">Set key.</param>
    /// <param name="value">Value to add to the set.</param>
    /// <returns>True if the value was added (wasn't already in the set).</returns>
    Task<bool> SetAddAsync(string key, string value);

    /// <summary>
    /// Removes a value from a Redis set.
    /// </summary>
    /// <param name="key">Set key.</param>
    /// <param name="value">Value to remove from the set.</param>
    /// <returns>True if the value was removed.</returns>
    Task<bool> SetRemoveAsync(string key, string value);

    /// <summary>
    /// Gets all members of a Redis set.
    /// </summary>
    /// <param name="key">Set key.</param>
    /// <returns>Array of set members.</returns>
    Task<string[]> GetSetMembersAsync(string key);

    /// <summary>
    /// Increments a numeric value atomically.
    /// </summary>
    /// <param name="key">Key containing numeric value.</param>
    /// <param name="value">Value to increment by (default 1).</param>
    /// <returns>The new value after incrementing.</returns>
    Task<long> IncrementAsync(string key, long value = 1);

    /// <summary>
    /// Gets multiple keys in a single operation.
    /// </summary>
    /// <param name="keys">Array of keys to retrieve.</param>
    /// <returns>Array of values corresponding to the keys (null for missing keys).</returns>
    Task<string?[]> GetMultipleAsync(string[] keys);

    /// <summary>
    /// Sets multiple key-value pairs in a single operation.
    /// </summary>
    /// <param name="keyValuePairs">Dictionary of key-value pairs to set.</param>
    /// <returns>True if all values were set successfully.</returns>
    Task<bool> SetMultipleAsync(Dictionary<string, string> keyValuePairs);

    /// <summary>
    /// Gets cache statistics and connection information.
    /// </summary>
    /// <returns>Cache service statistics.</returns>
    Task<CacheStats> GetStatsAsync();

    /// <summary>
    /// Checks if the Redis connection is healthy.
    /// </summary>
    /// <returns>True if connection is healthy.</returns>
    Task<bool> IsHealthyAsync();
}

/// <summary>
/// Redis-based implementation of cache service with high performance and reliability.
/// </summary>
public class CacheService : ICacheService, IDisposable
{
    private readonly ConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly ILogger<CacheService> _logger;
    private readonly string _keyPrefix;

    /// <summary>
    /// Default TTL for cached items (30 minutes).
    /// </summary>
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// JSON serialization options for consistent serialization.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CacheService(ConnectionMultiplexer connection, IConfiguration configuration, ILogger<CacheService> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _database = _connection.GetDatabase();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyPrefix = configuration.GetValue<string>("Cache:KeyPrefix") ?? "ev-analyzer:";
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var value = await _database.StringGetAsync(prefixedKey);
            
            if (value.HasValue)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return value;
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache value for key: {Key}", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var ttl = expiry ?? DefaultTtl;
            
            var success = await _database.StringSetAsync(prefixedKey, value, ttl);
            
            if (success)
            {
                _logger.LogDebug("Cache set for key: {Key} with TTL: {TTL}", key, ttl);
            }
            else
            {
                _logger.LogWarning("Failed to set cache value for key: {Key}", key);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cache value for key: {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectAsync<T>(string key) where T : class
    {
        try
        {
            var json = await GetAsync(key);
            
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var obj = JsonSerializer.Deserialize<T>(json, JsonOptions);
            _logger.LogDebug("Deserialized object of type {Type} from cache key: {Key}", typeof(T).Name, key);
            
            return obj;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached object for key: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached object for key: {Key}", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var success = await SetAsync(key, json, expiry);
            
            if (success)
            {
                _logger.LogDebug("Serialized and cached object of type {Type} for key: {Key}", typeof(T).Name, key);
            }

            return success;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize object for caching, key: {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cached object for key: {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var deleted = await _database.KeyDeleteAsync(prefixedKey);
            
            _logger.LogDebug("Cache delete for key: {Key}, existed: {Existed}", key, deleted);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete cache key: {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var exists = await _database.KeyExistsAsync(prefixedKey);
            
            _logger.LogDebug("Cache exists check for key: {Key}, exists: {Exists}", key, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if cache key exists: {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExpireAsync(string key, TimeSpan expiry)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var success = await _database.KeyExpireAsync(prefixedKey, expiry);
            
            _logger.LogDebug("Set expiration for key: {Key}, TTL: {TTL}, success: {Success}", key, expiry, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set expiration for cache key: {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<TimeSpan?> GetTtlAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var ttl = await _database.KeyTimeToLiveAsync(prefixedKey);
            
            _logger.LogDebug("TTL for key: {Key} is {TTL}", key, ttl);
            return ttl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get TTL for cache key: {Key}", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetAddAsync(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var added = await _database.SetAddAsync(prefixedKey, value);
            
            _logger.LogDebug("Set add for key: {Key}, value: {Value}, added: {Added}", key, value, added);
            return added;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add value to set, key: {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetRemoveAsync(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var removed = await _database.SetRemoveAsync(prefixedKey, value);
            
            _logger.LogDebug("Set remove for key: {Key}, value: {Value}, removed: {Removed}", key, value, removed);
            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove value from set, key: {Key}", key);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string[]> GetSetMembersAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var members = await _database.SetMembersAsync(prefixedKey);
            
            var result = members.Select(m => (string)m!).ToArray();
            _logger.LogDebug("Set members for key: {Key}, count: {Count}", key, result.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get set members for key: {Key}", key);
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public async Task<long> IncrementAsync(string key, long value = 1)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var newValue = await _database.StringIncrementAsync(prefixedKey, value);
            
            _logger.LogDebug("Increment for key: {Key}, by: {Value}, new value: {NewValue}", key, value, newValue);
            return newValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment value for key: {Key}", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string?[]> GetMultipleAsync(string[] keys)
    {
        if (keys == null || keys.Length == 0)
            throw new ArgumentException("Keys array cannot be null or empty", nameof(keys));

        try
        {
            var prefixedKeys = keys.Select(GetPrefixedKey).Cast<RedisKey>().ToArray();
            var values = await _database.StringGetAsync(prefixedKeys);
            
            var result = values.Select(v => v.HasValue ? (string?)v : null).ToArray();
            _logger.LogDebug("Multi-get for {KeyCount} keys, {HitCount} hits", keys.Length, result.Count(v => v != null));
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get multiple cache values");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetMultipleAsync(Dictionary<string, string> keyValuePairs)
    {
        if (keyValuePairs == null || keyValuePairs.Count == 0)
            throw new ArgumentException("Key-value pairs cannot be null or empty", nameof(keyValuePairs));

        try
        {
            var redisKeyValuePairs = keyValuePairs
                .Select(kvp => new KeyValuePair<RedisKey, RedisValue>(GetPrefixedKey(kvp.Key), kvp.Value))
                .ToArray();
            
            var success = await _database.StringSetAsync(redisKeyValuePairs);
            
            _logger.LogDebug("Multi-set for {KeyCount} keys, success: {Success}", keyValuePairs.Count, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set multiple cache values");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<CacheStats> GetStatsAsync()
    {
        try
        {
            var server = _connection.GetServer(_connection.GetEndPoints().First());
            var info = await server.InfoAsync();
            
            var stats = new CacheStats
            {
                IsConnected = _connection.IsConnected,
                DatabaseId = _database.Database,
                KeyPrefix = _keyPrefix,
                ConnectionString = _connection.Configuration ?? "Unknown",
                ServerVersion = server.Version.ToString(),
                TotalKeys = await server.DatabaseSizeAsync(_database.Database),
                UsedMemory = GetInfoValue(info, "used_memory"),
                ConnectedClients = GetInfoValue(info, "connected_clients"),
                TotalCommandsProcessed = GetInfoValue(info, "total_commands_processed"),
                Uptime = TimeSpan.FromSeconds(GetInfoValue(info, "uptime_in_seconds"))
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache statistics");
            return new CacheStats
            {
                IsConnected = _connection.IsConnected,
                DatabaseId = _database.Database,
                KeyPrefix = _keyPrefix,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Perform a simple ping operation
            var latency = await _database.PingAsync();
            var isHealthy = _connection.IsConnected && latency.TotalMilliseconds < 1000; // 1 second timeout
            
            _logger.LogDebug("Cache health check: connected={Connected}, latency={Latency}ms, healthy={Healthy}",
                _connection.IsConnected, latency.TotalMilliseconds, isHealthy);

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache health check failed");
            return false;
        }
    }

    /// <summary>
    /// Gets a prefixed cache key to avoid collisions.
    /// </summary>
    private string GetPrefixedKey(string key) => $"{_keyPrefix}{key}";

    /// <summary>
    /// Extracts a numeric value from Redis INFO command response.
    /// </summary>
    private static long GetInfoValue(IGrouping<string, KeyValuePair<string, string>>[] info, string key)
    {
        try
        {
            var serverInfo = info.FirstOrDefault(g => g.Key == "Server");
            var memoryInfo = info.FirstOrDefault(g => g.Key == "Memory");
            var clientsInfo = info.FirstOrDefault(g => g.Key == "Clients");
            var statsInfo = info.FirstOrDefault(g => g.Key == "Stats");

            var allInfo = serverInfo?.Concat(memoryInfo ?? Enumerable.Empty<KeyValuePair<string, string>>())
                                   .Concat(clientsInfo ?? Enumerable.Empty<KeyValuePair<string, string>>())
                                   .Concat(statsInfo ?? Enumerable.Empty<KeyValuePair<string, string>>());

            var value = allInfo?.FirstOrDefault(kvp => kvp.Key == key).Value;
            
            return long.TryParse(value, out var result) ? result : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Disposes the Redis connection.
    /// </summary>
    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Cache service statistics and health information.
/// </summary>
public class CacheStats
{
    public bool IsConnected { get; set; }
    public int DatabaseId { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = string.Empty;
    public long TotalKeys { get; set; }
    public long UsedMemory { get; set; }
    public long ConnectedClients { get; set; }
    public long TotalCommandsProcessed { get; set; }
    public TimeSpan Uptime { get; set; }
    public string? ErrorMessage { get; set; }

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            return $"Cache Error: {ErrorMessage}";
        }

        return $"Redis Cache: Connected={IsConnected}, Keys={TotalKeys:N0}, " +
               $"Memory={UsedMemory / 1024 / 1024:N0}MB, Version={ServerVersion}";
    }
}