using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Remora.Results;
using StackExchange.Redis;
// ReSharper disable TemplateIsNotCompileTimeConstantProblem

namespace RedisExplorer.Results;

/// <summary>
/// Extensions to <see cref="IRedisExplorer"/> providing <see cref="IResult{TEntity}"/> based methods.
/// </summary>
[PublicAPI]
public static class RedisExplorerSerializationResultExtensions
{
    /// <summary>Gets a value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>The result containing the located value or an error.</returns>
    public static Result<TValue> GetSerializedWithResult<TValue>(this IRedisExplorer redisExplorer, string key)
    {
        try
        {
            var res = redisExplorer.GetSerialized<TValue>(key);
            if (res is null)
                return new RedisError(RedisExplorerResultExtensions.NotFoundMessage, RedisErrorType.NotFound, key, null, null, null);
            
            return res;
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error getting key {Key}", key);
            return new RedisError(RedisExplorerResultExtensions.ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }

    /// <summary>Gets a value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation, containing the located value or null.</returns>
    public static async Task<Result<TValue>> GetSerializedWithResultAsync<TValue>(this IRedisExplorer redisExplorer, string key,
        CancellationToken token = default)
    {
        try
        {
            var res = await redisExplorer.GetSerializedAsync<TValue>(key, token);
            if (res is null)
                return new RedisError(RedisExplorerResultExtensions.NotFoundMessage, RedisErrorType.NotFound, key, null, null, null);
            
            return res;
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error getting key {Key}", key);
            return new RedisError(RedisExplorerResultExtensions.ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }

    private static string GetConditionalSetScript(IRedisExplorer redisExplorer)
        => redisExplorer.UsingPreExtendedCommandSet.HasValue 
            ? redisExplorer.UsingPreExtendedCommandSet.Value
                ? LuaScripts.ConditionalSetScriptPreExtendedSetCommand
                : LuaScripts.ConditionalSetScript
            : throw new InvalidOperationException("The information about whether the pre-extended set command is being used has not been set.");

    /// <summary>Sets a value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="errorIfExists">Whether return an error if the key already exist and abort setting.</param>
    public static Result SetSerializedWithResult<TValue>(this IRedisExplorer redisExplorer, string key, TValue value, DistributedCacheEntryOptions options, bool errorIfExists = true)
    {
        try
        {
            if (!errorIfExists)
            {
                redisExplorer.SetSerialized(key, value, options);

                return Result.Success;
            }

            var creationTime = redisExplorer.TimeProvider.GetUtcNow();

            var absoluteExpiration = redisExplorer.GetAbsoluteExpiration(creationTime, options);

            var redisDatabase = redisExplorer.GetDatabase();

            var result = redisDatabase.ScriptEvaluate(GetConditionalSetScript(redisExplorer), new[] { redisExplorer.Prefix.Append(key) },
                new RedisValue[]
                {
                    absoluteExpiration?.ToUnixTimeSeconds() ?? RedisExplorerResultExtensions.NotPresent,
                    options.SlidingExpiration?.TotalSeconds ?? RedisExplorerResultExtensions.NotPresent,
                    redisExplorer.GetExpirationInSeconds(creationTime, absoluteExpiration, options) ?? RedisExplorerResultExtensions.NotPresent,
                    redisExplorer.SerializeToUtf8Bytes(value)
                });

            if (!result.TryExtractString(out var resultString, out _, out _))
            {
                var str = result.Resp3Type.ToString();
                var message = RedisExplorerResultExtensions.GetUnexpectedMessage(null, null, "string", str);
                    
                redisExplorer.Logger.LogWarning(message);

                return new RedisError(message, RedisErrorType.UnexpectedResult, key, null, result.Resp3Type, null);
            }

            if (resultString == RedisExplorerResultExtensions.ExistsReturnValue)
            {
                return new RedisError(RedisExplorerResultExtensions.KeyAlreadyExistsMessage, RedisErrorType.KeyExists, key, resultString, result.Resp3Type, null);
            }
            
            if (resultString == RedisExplorerResultExtensions.NoDataReturnedSuccessValue)
            {
                return Result.Success;
            }
                
            var type = result.Resp3Type.ToString();
            var msg = RedisExplorerResultExtensions.GetUnexpectedMessage(RedisExplorerResultExtensions.ExpectedReturnValues, resultString, "string", type);
            
            redisExplorer.Logger.LogWarning(msg);
                
            return new RedisError(msg, RedisErrorType.UnexpectedResult, key, resultString, result.Resp3Type, null);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error setting key {Key}", key);
            return new RedisError(RedisExplorerResultExtensions.ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }
    
    /// <summary>Sets a value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    public static Result SetSerializedWithResult<TValue>(this IRedisExplorer redisExplorer, string key, TValue value, DistributedCacheEntryOptions options)
        => SetSerializedWithResult(redisExplorer, key, value, options, false);

    /// <summary>Sets the value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation.</returns>
    public static Task<Result> SetSerializedWithResultAsync<TValue>(this IRedisExplorer redisExplorer,
        string key,
        TValue value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
        => SetSerializedWithResultAsync(redisExplorer, key, value, options, false, token);

    /// <summary>Sets the value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="errorIfExists">Whether return an error if the key already exist and abort setting.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation.</returns>
    public static async Task<Result> SetSerializedWithResultAsync<TValue>(this IRedisExplorer redisExplorer,
        string key,
        TValue value,
        DistributedCacheEntryOptions options,
        bool errorIfExists,
        CancellationToken token = default)
    {
        try
        {
            if (!errorIfExists)
            {
                await redisExplorer.SetSerializedAsync(key, value, options, token);

                return Result.Success;
            }

            var creationTime = redisExplorer.TimeProvider.GetUtcNow();

            var absoluteExpiration = redisExplorer.GetAbsoluteExpiration(creationTime, options);

            var redisDatabase = await redisExplorer.GetDatabaseAsync();

            var result = await redisDatabase.ScriptEvaluateAsync(GetConditionalSetScript(redisExplorer),
                new[] { redisExplorer.Prefix.Append(key) },
                new RedisValue[]
                {
                    absoluteExpiration?.ToUnixTimeSeconds() ?? RedisExplorerResultExtensions.NotPresent,
                    options.SlidingExpiration?.TotalSeconds ?? RedisExplorerResultExtensions.NotPresent,
                    redisExplorer.GetExpirationInSeconds(creationTime, absoluteExpiration, options) ??
                    RedisExplorerResultExtensions.NotPresent,
                    redisExplorer.SerializeToUtf8Bytes(value)
                });

            if (!result.TryExtractString(out var resultString, out _, out _))
            {
                var str = result.Resp3Type.ToString();
                var message = RedisExplorerResultExtensions.GetUnexpectedMessage(null, null, "string", str);

                redisExplorer.Logger.LogWarning(message);

                return new RedisError(message, RedisErrorType.UnexpectedResult, key, null, result.Resp3Type, null);
            }

            if (resultString == RedisExplorerResultExtensions.ExistsReturnValue)
            {
                return new RedisError(RedisExplorerResultExtensions.KeyAlreadyExistsMessage, RedisErrorType.KeyExists, key, resultString,
                    result.Resp3Type, null);
            }

            if (resultString == RedisExplorerResultExtensions.NoDataReturnedSuccessValue)
            {
                return Result.Success;
            }

            var type = result.Resp3Type.ToString();
            var msg = RedisExplorerResultExtensions.GetUnexpectedMessage(RedisExplorerResultExtensions.ExpectedReturnValues, resultString, "string", type);

            redisExplorer.Logger.LogWarning(msg);

            return new RedisError(msg, RedisErrorType.UnexpectedResult, key, resultString, result.Resp3Type, null);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error setting key {Key}", key);
            return new RedisError(RedisExplorerResultExtensions.ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }
}
