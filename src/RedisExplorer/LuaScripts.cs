namespace RedisExplorer;

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
    // RETURNS 1
    // this order should not change LUA script depends on it
    /// <summary>
    /// The set script.
    /// </summary>
    public const string SetScript = """
                                    
                                                    local result = redis.call('HSET', KEYS[1], 'absexp', ARGV[1], 'sldexp', ARGV[2], 'data', ARGV[4])
                                                    if ARGV[3] ~= '-1' then
                                                      redis.call('EXPIRE', KEYS[1], ARGV[3])
                                                    end
                                                    
                                                    if result == 3 then
                                                      return '1'
                                                    end
                                                    
                                                    return '2'
                                    """;
    
    // KEYS[1] = = key
    // ARGV[1] = whether to return data or only refresh - 0 for no data, 1 to return data
    // RETURNS null if not found, 1 if successful and no data returned, or data if successful and data returned
    // this order should not change LUA script depends on it
    /// <summary>
    /// Script that HGET's the key's value, refreshes the expiration and returns the value or '1' (if return data arg was 0), if the key is not found NIL is returned.
    /// </summary>
    public const string GetAndRefreshScript = """
                                                              local sub = function (key)
                                                                local bulk = redis.call('HGETALL', key)
                                              	                 local result = {}
                                              	                 local nextkey
                                              	                 for i, v in ipairs(bulk) do
                                              		                 if i % 2 == 1 then
                                              			                 nextkey = v
                                              		                 else
                                              			                 result[nextkey] = v
                                              		                 end
                                              	                 end
                                              	                 return result
                                                              end
                                              
                                                              local result = sub(KEYS[1])
                                              
                                                              if next(result) == nil then
                                                                return nil
                                                              end
                                              
                                                              local sldexp = tonumber(result['sldexp'])
                                                              local absexp = tonumber(result['absexp'])
                                              
                                                              if sldexp == -1 then
                                                                if ARGV[1] == '1' then
                                                                  return result['data']
                                                                else
                                                                  return '1'
                                                                end
                                                              end
                                              
                                                              local time = tonumber(redis.call('TIME')[1])
                                                              local relexp = 1
                                                              if absexp ~= -1 then
                                                                relexp = absexp - time
                                                                if relexp <= 0 then
                                                                  return '1'
                                                                end
                                                              end
                                              
                                                              local exp = 1
                                                              if absexp ~= -1 then
                                                                if relexp <= sldexp then
                                                                  exp = relexp
                                                                else
                                                                  exp = sldexp
                                                                end
                                                              else
                                                                exp = sldexp
                                                              end
                                                              
                                                              redis.call('EXPIRE', KEYS[1], exp, 'XX')
                                                                              
                                                              if ARGV[1] == '1' then
                                                                return result['data']
                                                              end
                                                              return '1'
                                              """;
    
    // KEYS[1] = = key
    // RETURNS 1 if successful, or null if not found
    // this order should not change LUA script depends on it
    /// <summary>
    /// Script that refreshes the expiration and returns '1', if the key is not found NIL is returned.
    /// </summary>
    public const string RefreshScript = """
                                        
                                                        local sldexpResult = redis.call('HGET', KEYS[1], 'sldexp')
                                        
                                                        if sldexpResult == nil then
                                                          return nil
                                                        end
                                        
                                                        if sldexpResult == false then
                                                          return nil
                                                        end
                                        
                                                        local sldexp = tonumber(sldexpResult)
                                        
                                                        if sldexp == -1 then
                                                          return '1'
                                                        end
                                        
                                                        local absexp = tonumber(redis.call('HGET', KEYS[1], 'absexp'))
                                        
                                                        local time = tonumber(redis.call('TIME')[1])
                                                        local relexp = 1
                                                        if absexp ~= -1 then
                                                          relexp = absexp - time
                                                          if relexp <= 0 then
                                                            return '1'
                                                          end
                                                        end
                                        
                                                        local exp = 1
                                                        if absexp ~= -1 then
                                                          if relexp <= sldexp then
                                                            exp = relexp
                                                          else
                                                            exp = sldexp
                                                          end
                                                        else
                                                          exp = sldexp
                                                        end
                                                        
                                                        redis.call('EXPIRE', KEYS[1], exp, 'XX')
                                                                        
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
    /// Absolute exp key.
    /// </summary>
    public const string AbsoluteExpirationKey = "absexp";
    /// <summary>
    /// Sliding exp key.
    /// </summary>
    public const string SlidingExpirationKey = "sldexp";
    /// <summary>
    /// Data key.
    /// </summary>
    public const string DataKey = "data";
    /// <summary>
    /// Return data arg value.
    /// </summary>
    public const string ReturnDataArg = "1";
    /// <summary>
    /// Don't return data arg value.
    /// </summary>
    public const string DontReturnDataArg = "0";
    /// <summary>
    /// Successful result no data value.
    /// </summary>
    public const string NoDataReturnedSuccessValue = "1";
    /// <summary>
    /// Successful result no data value when a set method overwritten existing data.
    /// </summary>
    public const string NoDataReturnedSuccessSetOverwrittenValue = "2";
    /// <summary>
    /// Not present value.
    /// </summary>
    public const short NotPresent = -1;
    /// <summary>
    /// Not found value.
    /// </summary>
    public const string? NotFoundReturnValue = null;
}
