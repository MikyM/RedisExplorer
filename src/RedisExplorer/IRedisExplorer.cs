using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using RedLockNet;

namespace RedisExplorer;

/// <summary>
/// Represents a Redis cache.
/// </summary>
[PublicAPI]
public interface IRedisExplorer : IDistributedCache
{
    /// <summary>
    /// Whether pre extended command set is being used.
    /// </summary>
    /// <remarks>This will be null until first operation is made (or a connection is established).</remarks>
    bool? UsingPreExtendedCommandSet { get; }
    
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

    /// <summary>
    /// Calculates the hash of a script.
    /// </summary>
    /// <param name="script">Script.</param>
    /// <returns>Hash.</returns>
    string CalculateScriptHash(string script);
}
