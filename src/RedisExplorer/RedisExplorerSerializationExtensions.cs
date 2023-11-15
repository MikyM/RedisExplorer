using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace RedisExplorer;

/// <summary>
/// Extensions to <see cref="IRedisExplorer"/> providing methods supporting de/serialization.
/// </summary>
[PublicAPI]
public static class RedisExplorerSerializationExtensions
{
    /// <summary>
    /// Sets the value with the given key serializing it beforehand.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    public static Task SetAsync<TValue>(this IRedisExplorer explorer, string key, TValue value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        var serializedValue = TrySerialize(explorer, value);
        return explorer.SetAsync(key, serializedValue, options, token);
    }
    
    /// <summary>
    /// Sets the value with the given key serializing it beforehand. The <see cref="DistributedCacheEntryOptions"/> will be obtained from <see cref="RedisCacheExpirationOptions"/> based on the value type.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    public static Task SetAsync<TValue>(this IRedisExplorer explorer, string key, TValue value, CancellationToken token = default)
        => SetAsync(explorer, key, value, explorer.Options.ExpirationOptions.GetEntryOptions<TValue>(), token);
        
    /// <summary>
    /// Sets a value with the given key serializing it beforehand.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    public static void Set<TValue>(this IRedisExplorer explorer, string key, TValue value, DistributedCacheEntryOptions options)
    {
        var serializedValue = TrySerialize(explorer, value);
        explorer.Set(key, serializedValue, options);
    }
    
    /// <summary>
    /// Sets a value with the given key serializing it beforehand.
    /// The <see cref="DistributedCacheEntryOptions"/> will be obtained from <see cref="RedisCacheExpirationOptions"/> based on the value type>.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    public static void Set<TValue>(this IRedisExplorer explorer, string key, TValue value)
        => Set(explorer, key, value, explorer.Options.ExpirationOptions.GetEntryOptions<TValue>());
    
    /// <summary>
    /// Gets a value with the given key and deserializes it.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the located value or null.</returns>
    public static async Task<TValue?> GetAsync<TValue>(this IRedisExplorer explorer, string key, CancellationToken token = default)
    {
        var value = await explorer.GetAsync(key, token);
        return TryDeserialize<TValue>(explorer, value);
    }
    
    /// <summary>
    /// Gets a value with the given key and deserializes it.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>The located value or null.</returns>
    public static TValue? Get<TValue>(this IRedisExplorer explorer, string key)
    {
        var value = explorer.Get(key);
        return TryDeserialize<TValue>(explorer, value);
    }
    
    private static byte[] TrySerialize<TValue>(IRedisExplorer explorer, TValue value)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, explorer.JsonSerializerOptions);
        }
        catch (Exception ex)
        {
            explorer.Logger.LogError(ex, "Error serializing the object of type {Type}", typeof(TValue));
            throw;
        }
    }
    
    private static TValue? TryDeserialize<TValue>(IRedisExplorer explorer, byte[]? value)
    {
        if (value is null)
            return default;
        
        try
        {
            return JsonSerializer.Deserialize<TValue>(value, explorer.JsonSerializerOptions) ?? throw new JsonException($"Error deserializing the object of type {typeof(TValue).Name}");
        }
        catch (Exception ex)
        {
            explorer.Logger.LogError(ex, "Error deserializing the object of type {Type}", typeof(TValue).Name);
            throw;
        }
    }
}
