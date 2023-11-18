using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using RedLockNet;

namespace RedisExplorer;

/// <summary>
/// Represents a Redis cache.
/// </summary>
[PublicAPI]
public interface IRedisExplorer
{
    /// <summary>
    /// The prefix as a <see cref="RedisKey"/>.
    /// </summary>
    RedisKey Prefix { get; }
    
    /// <summary>
    /// The time provider.
    /// </summary>
    TimeProvider TimeProvider { get; }
    
    /// <summary>
    /// Gets the options.
    /// </summary>
    RedisCacheOptions Options { get; }
    
    /// <summary>
    /// Gets the json options.
    /// </summary>
    JsonSerializerOptions JsonSerializerOptions { get; }
        
    /// <summary>
    /// Gets the inner logger.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Whether the <see cref="ConnectionMultiplexer"/> is set to be proxy compliant.
    /// </summary>
    /// <remarks>This will be null until first operation is made (or a connection is established).</remarks>
    bool? UsingProxy { get; }

    /// <summary>
    /// The <see cref="ConfigurationOptions"/> used when creating the underlying <see cref="ConnectionMultiplexer"/>.
    /// </summary>
    /// <remarks>This will be null until first operation is made (or a connection is established).</remarks>
    ConfigurationOptions? ConfigurationOptions { get; }

    /// <summary>
    /// Gets the underlying Redis <see cref="IDatabase"/>.
    /// </summary>
    /// <returns>The underlying Redis database.</returns>
    IDatabase GetDatabase();
    
    /// <summary>
    /// Gets the underlying Redis <see cref="IDatabase"/>.
    /// </summary>
    /// <returns>The underlying Redis database.</returns>
    Task<IDatabase> GetDatabaseAsync();
    
    /// <summary>
    /// Gets the underlying <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    /// <returns>The underlying Redis connection multiplexer.</returns>
    IConnectionMultiplexer GetMultiplexer();
    
    /// <summary>
    /// Gets the underlying <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    /// <returns>The underlying Redis connection multiplexer.</returns>
    Task<IConnectionMultiplexer> GetMultiplexerAsync();
    
    /// <summary>
    /// Gets the underlying Redis <see cref="IDistributedLockFactory"/>.
    /// </summary>
    /// <returns>The underlying Redis lock factory.</returns>
    IDistributedLockFactory GetLockFactory();
    
    /// <summary>
    /// Gets the underlying Redis <see cref="IDistributedLockFactory"/>.
    /// </summary>
    /// <returns>The underlying Redis lock factory.</returns>
    Task<IDistributedLockFactory> GetLockFactoryAsync();
    
    /// <summary>
    /// Creates a lock.
    /// </summary>
    /// <param name="resource">The resource to lock.</param>
    /// <param name="expiryTime">The expiry time.</param>
    /// <returns>Created lock.</returns>
    IRedLock CreateLock(string resource, TimeSpan expiryTime);

    /// <summary>
    /// Creates a lock.
    /// </summary>
    /// <param name="resource">The resource to lock.</param>
    /// <param name="expiryTime">The expiry time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Created lock.</returns>
    Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a lock.
    /// </summary>
    /// <param name="resource">The resource to lock.</param>
    /// <param name="expiryTime">The expiry time.</param>
    /// <param name="waitTime">The wait time.</param>
    /// <param name="retryTime">The retry time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Created lock.</returns>
    IRedLock CreateLock(
        string resource,
        TimeSpan expiryTime,
        TimeSpan waitTime,
        TimeSpan retryTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a lock.
    /// </summary>
    /// <param name="resource">The resource to lock.</param>
    /// <param name="expiryTime">The expiry time.</param>
    /// <param name="waitTime">The wait time.</param>
    /// <param name="retryTime">The retry time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Created lock.</returns>
    Task<IRedLock> CreateLockAsync(
        string resource,
        TimeSpan expiryTime,
        TimeSpan waitTime,
        TimeSpan retryTime,
        CancellationToken cancellationToken = default);
            
    /// <summary>
    /// Gets a prefixed key.
    /// </summary>
    /// <param name="key">The key to prepend a prefix to.</param>
    /// <returns>The prefixed key.</returns>
    string GetPrefixedKey(string key);

    /// <summary>
    /// Gets the relative expiration in seconds.
    /// </summary>
    /// <param name="creationTime">Creation time.</param>
    /// <param name="absoluteExpiration">The absolute expiration.</param>
    /// <param name="options">The options.</param>
    /// <returns>The relative expiration, if any.</returns>
    long? GetExpirationInSeconds(DateTimeOffset creationTime, DateTimeOffset? absoluteExpiration, DistributedCacheEntryOptions options);

    /// <summary>
    /// Gets the absolute expiration.
    /// </summary>
    /// <param name="creationTime">Creation time.</param>
    /// <param name="options">The options.</param>
    /// <returns>The absolute expiration, if any.</returns>
    DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset creationTime, DistributedCacheEntryOptions options);

    /// <summary>Gets a value with the given key.</summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>A result of the operation as <see cref="IExplorerResult"/>.</returns>
    IExplorerResult<byte[]> Get(string key);

    /// <summary>Gets a value with the given key.</summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation, containing a result of the operation as <see cref="IExplorerResult"/>.</returns>
    Task<IExplorerResult<byte[]>> GetAsync(string key, CancellationToken token = default);

    /// <summary>Sets a value with the given key.</summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <returns>A result of the operation as <see cref="IExplorerResult"/>.</returns>
    IExplorerResult Set(string key, byte[] value, DistributedCacheEntryOptions options);

    /// <summary>Sets the value with the given key.</summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation, containing a result of the operation as <see cref="IExplorerResult"/>.</returns>
    Task<IExplorerResult> SetAsync(
      string key,
      byte[] value,
      DistributedCacheEntryOptions options,
      CancellationToken token = default);

    /// <summary>
    /// Refreshes a value in the cache based on its key, resetting its sliding expiration timeout (if any).
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>A result of the operation as <see cref="IExplorerResult"/>.</returns>
    IExplorerResult Refresh(string key);

    /// <summary>
    /// Refreshes a value in the cache based on its key, resetting its sliding expiration timeout (if any).
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation, containing a result of the operation as <see cref="IExplorerResult"/>.</returns>
    Task<IExplorerResult> RefreshAsync(string key, CancellationToken token = default);

    /// <summary>Removes the value with the given key.</summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>A result of the operation as <see cref="IExplorerResult"/>.</returns>
    IExplorerResult Remove(string key);

    /// <summary>Removes the value with the given key.</summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation, containing a result of the operation as <see cref="IExplorerResult"/>.</returns>
    Task<IExplorerResult> RemoveAsync(string key, CancellationToken token = default);

    /// <summary>
    /// Serializes the value using the underlying <see cref="RedisExplorer.JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <returns>The bytes.</returns>
    byte[] SerializeToUtf8Bytes<TValue>(TValue value) where TValue : class;

    /// <summary>
    /// Deserializes the bytes to the given type using the underlying <see cref="RedisExplorer.JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="bytes">The bytes to deserialize.</param>
    /// <typeparam name="TValue">The type of the value to deserialize the bytes to.</typeparam>
    /// <returns>The value.</returns>
    TValue Deserialize<TValue>(byte[] bytes) where TValue : class;

    /// <summary>
    /// Gets a value with the given key and deserializes it.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>The located value or null.</returns>
    IExplorerResult<TValue> Get<TValue>(string key) where TValue : class;

    /// <summary>
    /// Sets the value with the given key serializing it beforehand.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task<IExplorerResult> SetAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options,
        CancellationToken token = default) where TValue : class;

    /// <summary>
    /// Sets the value with the given key serializing it beforehand. The <see cref="DistributedCacheEntryOptions"/> will be obtained from <see cref="RedisCacheExpirationOptions"/> based on the value type.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task<IExplorerResult> SetAsync<TValue>(string key, TValue value, CancellationToken token = default) where TValue : class;

    /// <summary>
    /// Sets a value with the given key serializing it beforehand.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    IExplorerResult Set<TValue>(string key, TValue value, DistributedCacheEntryOptions options) where TValue : class;

    /// <summary>
    /// Sets a value with the given key serializing it beforehand.
    /// The <see cref="DistributedCacheEntryOptions"/> will be obtained from <see cref="RedisCacheExpirationOptions"/> based on the value type>.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    IExplorerResult Set<TValue>(string key, TValue value) where TValue : class;

    /// <summary>
    /// Gets a value with the given key and deserializes it.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the located value or null.</returns>
    Task<IExplorerResult<TValue>> GetAsync<TValue>(string key, CancellationToken token = default) where TValue : class;
}
