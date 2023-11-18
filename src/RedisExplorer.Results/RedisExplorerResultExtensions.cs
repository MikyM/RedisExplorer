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
public static class RedisExplorerResultExtensions
{
    internal const string NotFoundMessage = "The requested key was not found.";
    internal const string KeyAlreadyExistsMessage = "The key already exists.";
    internal const string ExceptionOccurredMessage = "An exception occurred while attempting to peform an operation against Redis.";
    internal const string NoDataReturnedSuccessValue = global::RedisExplorer.LuaScripts.NoDataReturnedSuccessValue;
    internal const short NotPresent = global::RedisExplorer.LuaScripts.NotPresent;
    internal const string ExistsReturnValue = LuaScripts.ExistsReturnValue;
    internal const string RefreshScript = global::RedisExplorer.LuaScripts.RefreshScript;
    internal const string ExpectedReturnValues = $"{NoDataReturnedSuccessValue}, {ExistsReturnValue} or null";
    internal static readonly RedisValue[] EmptyHashMembers = Array.Empty<RedisValue>();

    internal static string GetUnexpectedMessage(string? expectedValue, string? actualValue, string? expectedType, string? actualType)
        => $"Unexpected value returned from Redis script execution.{(expectedType is null ? "" : $"Expected a {expectedType} type. ")}{(actualType is null ? "" : $"Got {actualType} type. ")}{(expectedValue is null ? "" : $"Expected {expectedValue} value. ")}{(actualValue is null ? "" : $"Got {actualValue} value. ")}";
    
    /// <summary>Gets a value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>The result containing the located value or an error.</returns>
    public static Result<byte[]> GetWithResult(this IRedisExplorer redisExplorer, string key)
    {
        try
        {
            var res = redisExplorer.Get(key);
            if (res is null)
                return new RedisError(NotFoundMessage, RedisErrorType.NotFound, key, null, null, null);
            
            return res;
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error getting key {Key}", key);
            return new RedisError(ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }

    /// <summary>Gets a value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation, containing the located value or null.</returns>
    public static async Task<Result<byte[]?>> GetWithResultAsync(this IRedisExplorer redisExplorer, string key,
        CancellationToken token = default)
    {
        try
        {
            var res = await redisExplorer.GetAsync(key, token);
            if (res is null)
                return new RedisError(NotFoundMessage, RedisErrorType.NotFound, key, null, null, null);
            
            return res;
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error getting key {Key}", key);
            return new RedisError(ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
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
    public static Result SetWithResult(this IRedisExplorer redisExplorer, string key, byte[] value, DistributedCacheEntryOptions options, bool errorIfExists = true)
    {
        try
        {
            if (!errorIfExists)
            {
                redisExplorer.Set(key, value, options);

                return Result.Success;
            }

            var creationTime = redisExplorer.TimeProvider.GetUtcNow();

            var absoluteExpiration = redisExplorer.GetAbsoluteExpiration(creationTime, options);

            var redisDatabase = redisExplorer.GetDatabase();

            var result = redisDatabase.ScriptEvaluate(GetConditionalSetScript(redisExplorer), new[] { redisExplorer.Prefix.Append(key) },
                new RedisValue[]
                {
                    absoluteExpiration?.ToUnixTimeSeconds() ?? NotPresent,
                    options.SlidingExpiration?.TotalSeconds ?? NotPresent,
                    redisExplorer.GetExpirationInSeconds(creationTime, absoluteExpiration, options) ?? NotPresent,
                    value
                });

            if (!result.TryExtractString(out var resultString, out _, out _))
            {
                var str = result.Resp3Type.ToString();
                var message = GetUnexpectedMessage(null, null, "string", str);
                    
                redisExplorer.Logger.LogWarning(message);

                return new RedisError(message, RedisErrorType.UnexpectedResult, key, null, result.Resp3Type, null);
            }

            if (resultString == ExistsReturnValue)
            {
                return new RedisError(KeyAlreadyExistsMessage, RedisErrorType.KeyExists, key, resultString, result.Resp3Type, null);
            }
            
            if (resultString == NoDataReturnedSuccessValue)
            {
                return Result.Success;
            }
                
            var type = result.Resp3Type.ToString();
            var msg = GetUnexpectedMessage(ExpectedReturnValues, resultString, "string", type);
            
            redisExplorer.Logger.LogWarning(msg);
                
            return new RedisError(msg, RedisErrorType.UnexpectedResult, key, resultString, result.Resp3Type, null);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error setting key {Key}", key);
            return new RedisError(ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }
    
    /// <summary>Sets a value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    public static Result SetWithResult(this IRedisExplorer redisExplorer, string key, byte[] value, DistributedCacheEntryOptions options)
        => SetWithResult(redisExplorer, key, value, options, false);

    /// <summary>Sets the value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation.</returns>
    public static Task<Result> SetWithResultAsync(this IRedisExplorer redisExplorer,
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
        => SetWithResultAsync(redisExplorer, key, value, options, false, token);
        
    /// <summary>Sets the value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="errorIfExists">Whether return an error if the key already exist and abort setting.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation.</returns>
    public static async Task<Result> SetWithResultAsync(this IRedisExplorer redisExplorer, 
      string key,
      byte[] value,
      DistributedCacheEntryOptions options,
      bool errorIfExists,
      CancellationToken token = default)
    {
        try
        {
            if (!errorIfExists)
            {
                await redisExplorer.SetAsync(key, value, options, token);

                return Result.Success;
            }

            var creationTime = redisExplorer.TimeProvider.GetUtcNow();

            var absoluteExpiration = redisExplorer.GetAbsoluteExpiration(creationTime, options);

            var redisDatabase = await redisExplorer.GetDatabaseAsync();
            
            var result = await redisDatabase.ScriptEvaluateAsync(GetConditionalSetScript(redisExplorer), new[] { redisExplorer.Prefix.Append(key) },
                new RedisValue[]
                {
                    absoluteExpiration?.ToUnixTimeSeconds() ?? NotPresent,
                    options.SlidingExpiration?.TotalSeconds ?? NotPresent,
                    redisExplorer.GetExpirationInSeconds(creationTime, absoluteExpiration, options) ?? NotPresent,
                    value
                });

            if (!result.TryExtractString(out var resultString, out _, out _))
            {
                var str = result.Resp3Type.ToString();
                var message = GetUnexpectedMessage(null, null, "string", str);
                    
                redisExplorer.Logger.LogWarning(message);
                
                return new RedisError(message, RedisErrorType.UnexpectedResult, key, null, result.Resp3Type, null);
            }

            if (resultString == ExistsReturnValue)
            {
                return new RedisError(KeyAlreadyExistsMessage, RedisErrorType.KeyExists, key, resultString, result.Resp3Type, null);
            }
            
            if (resultString == NoDataReturnedSuccessValue)
            {
                return Result.Success;
            }
                
            var type = result.Resp3Type.ToString();
            var msg = GetUnexpectedMessage(ExpectedReturnValues, resultString, "string", type);
            
            redisExplorer.Logger.LogWarning(msg);
                
            return new RedisError(msg, RedisErrorType.UnexpectedResult, key, resultString, result.Resp3Type, null);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error setting key {Key}", key);
            return new RedisError(ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }

    /// <summary>
    /// Refreshes a value in the cache based on its key, resetting its sliding expiration timeout (if any).
    /// </summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="errorIfNotExists">Whether to return an error if the key doesn't exist.</param>
    public static Result RefreshWithResult(this IRedisExplorer redisExplorer, string key, bool errorIfNotExists)
    {
        try
        {
            if (!errorIfNotExists)
            {
                redisExplorer.Refresh(key);
                
                return Result.Success;
            }
            
            var redisDatabase = redisExplorer.GetDatabase();
            
            var result = redisDatabase.ScriptEvaluate(RefreshScript,
                new[] { redisExplorer.Prefix.Append(key) },
                EmptyHashMembers);

            if (result.IsNull)
            {
                return new RedisError(NotFoundMessage, RedisErrorType.NotFound, key, null, result.Resp3Type, null);
            }
            
            if (!result.TryExtractString(out var resultString, out _, out _))
            {
                var str = result.Resp3Type.ToString();
                var message = GetUnexpectedMessage(null, null, "string", str);
                    
                redisExplorer.Logger.LogWarning(message);
                
                return new RedisError(message, RedisErrorType.UnexpectedResult, key, null, result.Resp3Type, null);
            }
            
            if (resultString == NoDataReturnedSuccessValue)
            {
                return Result.Success;
            }
                
            var type = result.Resp3Type.ToString();
            var msg = GetUnexpectedMessage(ExpectedReturnValues, resultString, "string", type);
            
            redisExplorer.Logger.LogWarning(msg);
                
            return new RedisError(msg, RedisErrorType.UnexpectedResult, key, resultString, result.Resp3Type, null);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error refreshing key {Key}", key);
            return new RedisError(ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }
    
    /// <summary>
    /// Refreshes a value in the cache based on its key, resetting its sliding expiration timeout (if any).
    /// </summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    public static Result RefreshWithResult(this IRedisExplorer redisExplorer, string key)
        => RefreshWithResult(redisExplorer, key, false);

    /// <summary>
    /// Refreshes a value in the cache based on its key, resetting its sliding expiration timeout (if any).
    /// </summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="errorIfNotExists">Whether to return an error if the key doesn't exist.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation.</returns>
    public static async Task<Result> RefreshWithResultAsync(this IRedisExplorer redisExplorer, string key, bool errorIfNotExists, CancellationToken token = default)
    {
        try
        {
            if (!errorIfNotExists)
            {
                await redisExplorer.RefreshAsync(key, token);
                
                return Result.Success;
            }
            
            var redisDatabase = await redisExplorer.GetDatabaseAsync();
            
            var result = await redisDatabase.ScriptEvaluateAsync(RefreshScript,
                new[] { redisExplorer.Prefix.Append(key) },
                EmptyHashMembers);

            if (result.IsNull)
            {
                return new RedisError(NotFoundMessage, RedisErrorType.NotFound, key, null, result.Resp3Type, null);
            }
            
            if (!result.TryExtractString(out var resultString, out _, out _))
            {
                var str = result.Resp3Type.ToString();
                var message = GetUnexpectedMessage(null, null, "string", str);
                    
                redisExplorer.Logger.LogWarning(message);
                
                return new RedisError(message, RedisErrorType.UnexpectedResult, key, null, result.Resp3Type, null);
            }
            
            if (resultString == NoDataReturnedSuccessValue)
            {
                return Result.Success;
            }
                
            var type = result.Resp3Type.ToString();
            var msg = GetUnexpectedMessage(ExpectedReturnValues, resultString, "string", type);
            
            redisExplorer.Logger.LogWarning(msg);
                
            return new RedisError(msg, RedisErrorType.UnexpectedResult, key, resultString, result.Resp3Type, null);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error refreshing key {Key}", key);
            return new RedisError(ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }
    
    /// <summary>
    /// Refreshes a value in the cache based on its key, resetting its sliding expiration timeout (if any).
    /// </summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation.</returns>
    public static Task<Result> RefreshWithResultAsync(this IRedisExplorer redisExplorer, string key, CancellationToken token = default)
        => RefreshWithResultAsync(redisExplorer, key, false, token);

    /// <summary>Removes the value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="errorIfNotExists">Whether to return an error if the key doesn't exist.</param>
    public static Result RemoveWithResult(this IRedisExplorer redisExplorer, string key, bool errorIfNotExists)
    {
        try
        {
            if (!errorIfNotExists)
            {
                redisExplorer.Remove(key);
                
                return Result.Success;
            }

            var redisDatabase = redisExplorer.GetDatabase();
            
            var result = redisDatabase.ScriptEvaluate(LuaScripts.RemoveScript,
                new[] { redisExplorer.Prefix.Append(key) },
                EmptyHashMembers);

            if (result.IsNull)
            {
                return new RedisError(NotFoundMessage, RedisErrorType.NotFound, key, null, result.Resp3Type, null);
            }
            
            if (!result.TryExtractString(out var resultString, out _, out _))
            {
                var str = result.Resp3Type.ToString();
                var message = GetUnexpectedMessage(null, null, "string", str);
                    
                redisExplorer.Logger.LogWarning(message);
                
                return new RedisError(message, RedisErrorType.UnexpectedResult, key, null, result.Resp3Type, null);
            }
            
            if (resultString == NoDataReturnedSuccessValue)
            {
                return Result.Success;
            }
                
            var type = result.Resp3Type.ToString();
            var msg = GetUnexpectedMessage(ExpectedReturnValues, resultString, "string", type);
            
            redisExplorer.Logger.LogWarning(msg);
                
            return new RedisError(msg, RedisErrorType.UnexpectedResult, key, resultString, result.Resp3Type, null);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error removing key {Key}", key);
            return new RedisError(ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }
    
    /// <summary>Removes the value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    public static Result RemoveWithResult(this IRedisExplorer redisExplorer, string key)
        => RemoveWithResult(redisExplorer, key, false);

    /// <summary>Removes the value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="errorIfNotExists">Whether to return an error if the key doesn't exist.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation.</returns>
    public static async Task<Result> RemoveWithResultAsync(this IRedisExplorer redisExplorer, string key, bool errorIfNotExists, CancellationToken token = default)
    {
        try
        {
            if (!errorIfNotExists)
            {
                await redisExplorer.RemoveAsync(key, token);
                
                return Result.Success;
            }

            var redisDatabase = await redisExplorer.GetDatabaseAsync();
            
            var result = await redisDatabase.ScriptEvaluateAsync(LuaScripts.RemoveScript,
                new[] { redisExplorer.Prefix.Append(key) },
                EmptyHashMembers);

            if (result.IsNull)
            {
                return new RedisError(NotFoundMessage, RedisErrorType.NotFound, key, null, result.Resp3Type, null);
            }
            
            if (!result.TryExtractString(out var resultString, out _, out _))
            {
                var str = result.Resp3Type.ToString();
                var message = GetUnexpectedMessage(null, null, "string", str);
                    
                redisExplorer.Logger.LogWarning(message);
                
                return new RedisError(message, RedisErrorType.UnexpectedResult, key, null, result.Resp3Type, null);
            }
            
            if (resultString == NoDataReturnedSuccessValue)
            {
                return Result.Success;
            }
                
            var type = result.Resp3Type.ToString();
            var msg = GetUnexpectedMessage(ExpectedReturnValues, resultString, "string", type);
            
            redisExplorer.Logger.LogWarning(msg);
                
            return new RedisError(msg, RedisErrorType.UnexpectedResult, key, resultString, result.Resp3Type, null);
        }
        catch (Exception ex)
        {
            redisExplorer.Logger.LogError(ex, "Error removing key {Key}", key);
            return new RedisError(ExceptionOccurredMessage, RedisErrorType.Unknown, key, null, null, ex);
        }
    }
    
    /// <summary>Removes the value with the given key.</summary>
    /// <param name="redisExplorer">The explorer instance.</param>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="token">Optional. The <see cref="T:System.Threading.CancellationToken" /> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous operation.</returns>
    public static Task<Result> RemoveWithResultAsync(this IRedisExplorer redisExplorer, string key,CancellationToken token = default)
        => RemoveWithResultAsync(redisExplorer, key, false, token);
}
