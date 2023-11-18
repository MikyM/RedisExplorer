using JetBrains.Annotations;

namespace RedisExplorer.Results;

/// <summary>
/// The Lua scripts used by RedisExplorer.
/// </summary>
[PublicAPI]
public static class LuaScripts
{
    //
    // -- Explanation of why two kinds of SetScript are used --
    // * Redis 2.0 had HSET key field value for setting individual hash fields,
    // and HMSET key field value [field value ...] for setting multiple hash fields (against the same key).
    // * Redis 4.0 added HSET key field value [field value ...] and deprecated HMSET.
    //
    // On Redis versions that don't have the newer HSET variant, we use SetScriptPreExtendedSetCommand
    // which uses the (now deprecated) HMSET.

    // KEYS[1] = = key
    // ARGV[1] = absolute-expiration - in unix time seconds as long (-1 for none)
    // ARGV[2] = sliding-expiration - in seconds as long (-1 for none)
    // ARGV[3] = relative-expiration (long, in seconds, -1 for none) - Min(absolute-expiration - Now, sliding-expiration)
    // ARGV[4] = data - byte[]
    // RETURNS 1 if set, 0 if not set
    // this order should not change LUA script depends on it
    /// <summary>
    /// The set script.
    /// </summary>
    public const string ConditionalSetScript = """
                                               local exists = redis.call('EXISTS', KEYS[1])
                                               if exists == 1 then
                                                  return '0'
                                               end
                                                   
                                               redis.call('HSET', KEYS[1], 'absexp', ARGV[1], 'sldexp', ARGV[2], 'data', ARGV[4])
                                               if ARGV[3] ~= '-1' then
                                                  redis.call('EXPIRE', KEYS[1], ARGV[3])
                                               end
                                               return '1'
                                               """;

    /// <summary>
    /// The pre extended set script.
    /// </summary>
    public const string ConditionalSetScriptPreExtendedSetCommand = """
                                                                    local exists = redis.call('EXISTS', KEYS[1])
                                                                    if exists == 1 then
                                                                       return '0'
                                                                    end
                                                                                   
                                                                    redis.call('HMSET', KEYS[1], 'absexp', ARGV[1], 'sldexp', ARGV[2], 'data', ARGV[4])
                                                                    if ARGV[3] ~= '-1' then
                                                                       redis.call('EXPIRE', KEYS[1], ARGV[3])
                                                                    end
                                                                    return '1'
                                                                    """;
    
    /// <summary>
    /// The conditional remove script.
    /// </summary>
    // KEYS[1] = = key
    // RETURNS null if not exists, 1 if removed.
    public const string RemoveScript = """
                                                                    local removedCount = redis.call('UNLINK', KEYS[1])
                                                                    if removedCount >= 1 then
                                                                       return '1'
                                                                    end
                                                                                   
                                                                    return nil
                                                                    """;

    /// <summary>
    /// Exists return value.
    /// </summary>
    public const string ExistsReturnValue = "0";
}
