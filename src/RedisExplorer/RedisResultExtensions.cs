using System.Diagnostics.CodeAnalysis;

namespace RedisExplorer;

/// <summary>
/// Redis extensions.
/// </summary>
[PublicAPI]
public static class RedisResultExtensions
{
    /// <summary>
    /// Attempts to extract a string from a <see cref="RedisResult"/>.
    /// </summary>
    /// <remarks>This catches and swallows any exceptions that could be thrown.</remarks>
    /// <param name="redisResult">A redis result.</param>
    /// <param name="extracted">Extracted string</param>
    /// <param name="type">Type of the string.</param>
    /// <param name="isErrorString">Whether the string is an error string.</param>
    /// <returns>True if extraction was successful, otherwise false.</returns>
    public static bool TryExtractString(this RedisResult redisResult, [NotNullWhen(true)] out string? extracted, out string? type, out bool isErrorString)
    {
        extracted = null;
        type = null;
        
        isErrorString = false;
        
        try
        {
            if (redisResult.IsNull)
                return false;

            if (redisResult.Resp3Type is ResultType.Null)
                return false;

            isErrorString = redisResult.Resp3Type == ResultType.Error;
            
            if (redisResult.Resp3Type is not ResultType.SimpleString and not ResultType.BulkString and not ResultType.VerbatimString and not ResultType.Error)
                return false;

            extracted = redisResult.ToString(out type);

            return extracted is not null;
        }
        catch
        {
            return false;
        }
    }
}
