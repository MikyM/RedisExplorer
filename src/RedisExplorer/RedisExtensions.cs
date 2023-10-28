﻿using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;

namespace RedisExplorer;

/// <summary>
/// Redis extensions.
/// </summary>
internal static class RedisExtensions
{
    /// <summary>
    /// Attempts to extract a string from a <see cref="RedisResult"/>.
    /// </summary>
    /// <param name="redisResult">A redis result.</param>
    /// <param name="extracted">Extracted string</param>
    /// <returns>True if extraction was successful, false if not.</returns>
    internal static bool TryExtractString(this RedisResult redisResult, [NotNullWhen(true)] out string? extracted)
    {
        extracted = null;
        
        if (redisResult.IsNull)
            return false;
        if (redisResult.Type is not (ResultType.SimpleString or ResultType.BulkString))
            return false;

        extracted = (string?)redisResult;

        return extracted is not null;
    }
}
