using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace RedisExplorer;

/// <summary>
/// Represents a Redis cache.
/// </summary>
[PublicAPI]
public interface IRedisExplorer : IDistributedCache
{
    /// <summary>
    /// The options.
    /// </summary>
    RedisCacheOptions Options { get; }
    
    /// <summary>
    /// The json options.
    /// </summary>
    JsonSerializerOptions JsonSerializerOptions { get; }
        
    /// <summary>
    /// The inner logger.
    /// </summary>
    ILogger Logger { get; }
    
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
    IDistributedLock CreateLock(string resource, TimeSpan expiryTime);

    /// <summary>
    /// Creates a lock.
    /// </summary>
    /// <param name="resource">The resource to lock.</param>
    /// <param name="expiryTime">The expiry time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Created lock.</returns>
    Task<IDistributedLock> CreateLockAsync(string resource, TimeSpan expiryTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a lock.
    /// </summary>
    /// <param name="resource">The resource to lock.</param>
    /// <param name="expiryTime">The expiry time.</param>
    /// <param name="waitTime">The wait time.</param>
    /// <param name="retryTime">The retry time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Created lock.</returns>
    IDistributedLock CreateLock(
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
    Task<IDistributedLock> CreateLockAsync(
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
}
