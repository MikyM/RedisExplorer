using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Remora.Results;

namespace RedisExplorer.Results;

/// <summary>
/// Extensions to <see cref="IRedisExplorer"/> providing <see cref="IResult{TEntity}"/> based methods.
/// </summary>
[PublicAPI]
public static class RedisExplorerExtensions
{
    /// <summary>Gets a value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>The result containing the located value or an error.</returns>
    public static Result<byte[]> Get(this IRedisExplorer redisExplorer, string key)
    {
        try
        {
            return redisExplorer.Get(key);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error getting key {Key}", key);
            return ex;
        }
    }
}
