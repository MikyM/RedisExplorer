using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;

namespace RedisExplorer.Unit.Tests;

public class RedisExplorerShould
{
    private IRedisExplorer RedisExplorer
    {
        get
        {
            var cacheOpt = new RedisCacheOptions();
            var cacheOptMock = new Mock<IOptions<RedisCacheOptions>>();
            cacheOptMock.SetupGet(x => x.Value).Returns(cacheOpt);
        
            var jsonOpt = new JsonSerializerOptions();
            var jsonOptMock = new Mock<IOptionsMonitor<JsonSerializerOptions>>();
            jsonOptMock.Setup(x => x.Get(global::RedisExplorer.RedisExplorer.JsonOptionsName)).Returns(jsonOpt);

            var redisExplorerOpt = new RedisExplorerOptions();
            var redisExplorerOptMock = new Mock<IOptions<RedisExplorerOptions>>();
            redisExplorerOptMock.Setup(x => x.Value).Returns(redisExplorerOpt);
            var immutableOpt = new ImmutableRedisExplorerOptions(redisExplorerOptMock.Object);
        
            return new RedisExplorerImpl(cacheOptMock.Object, jsonOptMock.Object, immutableOpt);
        }
    }
}
