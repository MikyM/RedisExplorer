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
    // ARGV[5] = 1 to check if key exists and abort if it does, 0 to not check
    // RETURNS 1 if set, 2 if set and overwritten, 3 if not set due to key collision
    // this order should not change LUA script depends on it
    /// <summary>
    /// The set script.
    /// </summary>
    public const string SetScript = """
                                                    if ARGV[5] == '1' then
                                                      if redis.call('EXISTS', KEYS[1]) == 1 then
                                                        return '3'
                                                      end
                                                    end
                                                    
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
    // ARGV[1] = whether to also refresh the expiration (1 or 0)
    // RETURNS null if not found, 1 if successful and no data returned, or data if successful and data returned
    // this order should not change LUA script depends on it
    /// <summary>
    /// Script that HGET's the key's value, refreshes the expiration and returns the value, if the key is not found NIL is returned.
    /// </summary>
    public const string GetAndRefreshScript = """
                                                              local hgetall = function (key)
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
                                              
                                                              local result = hgetall(KEYS[1])
                                              
                                                              if next(result) == nil then
                                                                return nil
                                                              end
                                              
                                                              local absexp = tonumber(result['absexp'])
                                                              local sldexp = tonumber(result['sldexp'])
                                                              local data = result['data']
                                                              
                                                              if ARGV[1] == -1 then
                                                                  return data
                                                              end
                                              
                                                              if sldexp == -1 then
                                                                  return data
                                                              end
                                              
                                                              local time = tonumber(redis.call('TIME')[1])
                                                              local relexp = 1
                                                              if absexp ~= -1 then
                                                                relexp = absexp - time
                                                                if relexp <= 0 then
                                                                  return data
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
                                                              
                                                              local expire = redis.call('EXPIRE', KEYS[1], exp, 'XX')
                                                              if expire == 1 then
                                                                return data
                                                              end
                                                              
                                                              redis.call('HSET', KEYS[1], 'absexp', absexp, 'sldexp', sldexp, 'data', data)
                                                              redis.call('EXPIRE', KEYS[1], exp)
                                                              
                                                              return data
                                              """;
    
    // KEYS[1] = = key
    // RETURNS 1 if successful, or null if not found
    // this order should not change LUA script depends on it
    /// <summary>
    /// Script that refreshes the expiration and returns '1', if the key is not found NIL is returned.
    /// </summary>
    public const string RefreshScript = """
                                                        local result = redis.call('HMGET', KEYS[1], 'absexp', 'sldexp')

                                                        if result[1] == nil then
                                                          return nil
                                                        end
     
                                                        local absexp = tonumber(result[1])                
                                                        local sldexp = tonumber(result[2])

                                                        if sldexp == -1 then
                                                          return '4'
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
                                                        
                                                        local expire = redis.call('EXPIRE', KEYS[1], exp, 'XX')
                                                        if expire == 1 then
                                                          return '1'
                                                        end
                                                        
                                                        return nil
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
    /// Refresh key expiration on GET arg.
    /// </summary>
    public const string GetWithRefreshArg = "1";
    /// <summary>
    /// Abort if exists.
    /// </summary>
    public const string WithoutKeyOverwriteArg = "1";
    /// <summary>
    /// Successful result no data value.
    /// </summary>
    public const string SuccessReturn = "1";
    /// <summary>
    /// Successful result no data value when a set method overwritten existing data.
    /// </summary>
    public const string SetOverwrittenReturn = "2";
    /// <summary>
    /// Successful result no data value when a set method collided with existing key.
    /// </summary>
    public const string SetCollisionReturn = "3";
    /// <summary>
    /// No sliding exp on refresh.
    /// </summary>
    public const string RefreshNoSlidingExpirationReturn = "4";
    /// <summary>
    /// Not present value.
    /// </summary>
    public const short NotPresent = -1;
    /// <summary>
    /// Not found value.
    /// </summary>
    public const string? NotFoundReturnValue = null;
}
