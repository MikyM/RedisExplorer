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
    public static Task SetSerializedAsync<TValue>(this IRedisExplorer explorer, string key, TValue value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        var serializedValue = SerializeToUtf8Bytes(explorer, value);
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
    public static Task SetSerializedAsync<TValue>(this IRedisExplorer explorer, string key, TValue value, CancellationToken token = default)
        => SetSerializedAsync(explorer, key, value, explorer.Options.ExpirationOptions.GetEntryOptions<TValue>(), token);
        
    /// <summary>
    /// Sets a value with the given key serializing it beforehand.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    public static void SetSerialized<TValue>(this IRedisExplorer explorer, string key, TValue value, DistributedCacheEntryOptions options)
    {
        var serializedValue = SerializeToUtf8Bytes(explorer, value);
        explorer.Set(key, serializedValue, options);
    }
    
    /// <summary>
    /// Sets a value with the given key serializing it beforehand.
    /// The <see cref="DistributedCacheEntryOptions"/> will be obtained from <see cref="RedisCacheExpirationOptions"/> based on the value type>.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    public static void SetSerialized<TValue>(this IRedisExplorer explorer, string key, TValue value)
        => SetSerialized(explorer, key, value, explorer.Options.ExpirationOptions.GetEntryOptions<TValue>());
    
    /// <summary>
    /// Gets a value with the given key and deserializes it.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the located value or null.</returns>
    public static async Task<TValue?> GetSerializedAsync<TValue>(this IRedisExplorer explorer, string key, CancellationToken token = default)
    {
        var value = await explorer.GetAsync(key, token);

        if (value is null)
        {
            return default;
        }
        
        return Deserialize<TValue>(explorer, value);
    }
    
    /// <summary>
    /// Gets a value with the given key and deserializes it.
    /// </summary>
    /// <param name="explorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>The located value or null.</returns>
    public static TValue? GetSerialized<TValue>(this IRedisExplorer explorer, string key)
    {
        var value = explorer.Get(key);
        
        if (value is null)
        {
            return default;
        }

        return Deserialize<TValue>(explorer, value);
    }
    
    /// <summary>
    /// Serializes the value using the underlying <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <param name="value">The value to serialize.</param>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <returns>The bytes.</returns>
    public static byte[] SerializeToUtf8Bytes<TValue>(this IRedisExplorer explorer, TValue value)
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
    
    /// <summary>
    /// Deserializes the bytes to the given type using the underlying <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="explorer">The explorer.</param>
    /// <param name="bytes">The bytes to deserialize.</param>
    /// <typeparam name="TValue">The type of the value to deserialize the bytes to.</typeparam>
    /// <returns>The value.</returns>
    public static TValue Deserialize<TValue>(this IRedisExplorer explorer, byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<TValue>(bytes, explorer.JsonSerializerOptions) ?? throw new JsonException("The deserialized value is null.");
        }
        catch (Exception ex)
        {
            explorer.Logger.LogError(ex, "Error deserializing the object of type {Type}", typeof(TValue).Name);
            throw;
        }
    }
}
