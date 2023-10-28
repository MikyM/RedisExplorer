using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Distributed;
using NRedisStack;
using RedLockNet;

namespace RedisExplorer;

/// <summary>
/// Represents a Redis cache.
/// </summary>
[PublicAPI]
public interface IRedisExplorer : IDistributedCache
{
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
    /// <returns>Created lock.</returns>
    Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime);

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
        CancellationToken? cancellationToken = null);

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
        CancellationToken? cancellationToken = null);
            
    /// <summary>
    /// Gets a prefixed key.
    /// </summary>
    /// <param name="key">The key to prepend a prefix to.</param>
    /// <returns>The prefixed key.</returns>
    string GetPrefixedKey(string key);
    
    /// <summary>
    /// Gets <see cref="IJsonCommands"/>.
    /// </summary>
    /// <returns>An instance of <see cref="IJsonCommands"/>.</returns>
    IJsonCommands GetJsonCommands();
    /// <summary>
    /// Gets <see cref="IJsonCommandsAsync"/>.
    /// </summary>
    /// <returns>An instance of <see cref="IJsonCommandsAsync"/>.</returns>
    IJsonCommandsAsync GetJsonCommandsAsync();
    /// <summary>
    /// Gets <see cref="IBloomCommands"/>.
    /// </summary>
    /// <returns>An instance of <see cref="IBloomCommands"/>.</returns>
    IBloomCommands GetBloomCommands();
    /// <summary>
    /// Gets <see cref="IBloomCommandsAsync"/>.
    /// </summary>
    /// <returns>An instance of <see cref="IBloomCommandsAsync"/>.</returns>
    IBloomCommandsAsync GetBloomCommandsAsync();
    /// <summary>
    /// Gets <see cref="ICmsCommands"/>.
    /// </summary>
    /// <returns>An instance of <see cref="ICmsCommands"/>.</returns>
    ICmsCommands GetCmsCommands();
    /// <summary>
    /// Gets <see cref="ICmsCommandsAsync"/>.
    /// </summary>
    /// <returns>An instance of <see cref="ICmsCommandsAsync"/>.</returns>
    ICmsCommandsAsync GetCmsCommandsAsync();
    /// <summary>
    /// Gets <see cref="ICuckooCommands"/>.
    /// </summary>
    /// <returns>An instance of <see cref="ICuckooCommands"/>.</returns>
    ICuckooCommands GetCuckooCommands();
    /// <summary>
    /// Gets <see cref="ICuckooCommandsAsync"/>.
    /// </summary>
    /// <returns>An instance of <see cref="ICuckooCommandsAsync"/>.</returns>
    ICuckooCommandsAsync GetCuckooCommandsAsync();
    /// <summary>
    /// Gets <see cref="ISearchCommands"/>.
    /// </summary>
    /// <param name="defaultDialect">The default dialect, if any.</param>
    /// <returns>An instance of <see cref="ISearchCommands"/>.</returns>
    ISearchCommands GetSearchCommands(int? defaultDialect = null);
    /// <summary>
    /// Gets <see cref="ISearchCommandsAsync"/>.
    /// </summary>
    /// <returns>An instance of <see cref="ISearchCommandsAsync"/>.</returns>
    ISearchCommandsAsync GetSearchCommandsAsync();
    /// <summary>
    /// Gets <see cref="ITdigestCommands"/>.
    /// </summary>
    /// <returns>An instance of <see cref="ITdigestCommands"/>.</returns>
    ITdigestCommands GetTdigestCommands();
    /// <summary>
    /// Gets <see cref="ITdigestCommandsAsync"/>.
    /// </summary>
    /// <returns>An instance of <see cref="ITdigestCommandsAsync"/>.</returns>
    ITdigestCommandsAsync GetTdigestCommandsAsync();

    /// <summary>
    /// Sets the value with the given key serializing it beforehand.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task SetAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options,
        CancellationToken token = default);
    
    /// <summary>
    /// Sets the value with the given key serializing it beforehand. The <see cref="DistributedCacheEntryOptions"/> will be obtained from <see cref="RedisExplorerOptions"/> based on the value type.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task SetAsync<TValue>(string key, TValue value, CancellationToken token = default);
    
    /// <summary>
    /// Sets a value with the given key serializing it beforehand.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    void Set<TValue>(string key, TValue value, DistributedCacheEntryOptions options);
    
    /// <summary>
    /// Sets a value with the given key serializing it beforehand.
    /// The <see cref="DistributedCacheEntryOptions"/> will be obtained from <see cref="RedisExplorerOptions"/> based on the value type>.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    void Set<TValue>(string key, TValue value);

    /// <summary>
    /// Gets a value with the given key and deserializes it.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the located value or null.</returns>
    Task<TValue> GetAsync<TValue>(string key, CancellationToken token = default);
    
    /// <summary>
    /// Gets a value with the given key and deserializes it.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>The located value or null.</returns>
    TValue Get<TValue>(string key);
}
