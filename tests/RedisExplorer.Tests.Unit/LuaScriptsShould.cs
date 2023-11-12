using FluentAssertions;

namespace RedisExplorer.Tests.Unit;

public class LuaScriptsShould
{
    private const string GetAndRefreshScriptTestScript = (@"
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
                  if relexp <= 0
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
                return '1'");
    
    
    private const string RefreshTestScript = (@"
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
                  if relexp <= 0
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
                                
                return '1'");
    
    private const string SetTestScript = (@"
                redis.call('HSET', KEYS[1], 'absexp', ARGV[1], 'sldexp', ARGV[2], 'data', ARGV[4])
                if ARGV[3] ~= '-1' then
                  redis.call('EXPIRE', KEYS[1], ARGV[3])
                end
                return 1");

    private const string SetScriptPreExtendedSetTestCommand = (@"
                redis.call('HMSET', KEYS[1], 'absexp', ARGV[1], 'sldexp', ARGV[2], 'data', ARGV[4])
                if ARGV[3] ~= '-1' then
                  redis.call('EXPIRE', KEYS[1], ARGV[3])
                end
                return 1");

    [Fact]
    public void Return_correct_get_and_refresh_script()
    {
        const string script = LuaScripts.GetAndRefreshScript;
        script.Should().Be(GetAndRefreshScriptTestScript);
    }

    [Fact]
    public void Return_correct_refresh_script()
    {
        const string script = LuaScripts.RefreshScript;
        script.Should().Be(RefreshTestScript);
    }

    [Fact]
    public void Return_correct_set_script()
    {
        const string script = LuaScripts.SetScript;
        script.Should().Be(SetTestScript);
    }

    [Fact]
    public void Return_correct_pre_extended_commands_set_script()
    {
        const string script = LuaScripts.SetScriptPreExtendedSetCommand;
        script.Should().Be(SetScriptPreExtendedSetTestCommand);
    }
}
