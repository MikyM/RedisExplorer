using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedLockNet;
using StackExchange.Redis;

namespace RedisExplorer.Tests.Unit;

[UsedImplicitly]
public class DistributedCacheTests
{
    [UsedImplicitly]
    public class Fixture
    {
        public JsonSerializerOptions GetJsonOptions() => new();
        public Mock<IDatabase> GetDatabaseMock() => new();
        public Mock<IConnectionMultiplexer> GetMultiplexerMock() => new();
        public Mock<IDistributedLockFactory> GetLockFactoryMock() => new();
        public Mock<TimeProvider> GetTimeProviderMock() => new();

        public string TestString => "testString";
        public string TestKey => "testKey";

        public IDistributedCache GetTestInstance(IDatabase database, Mock<IConnectionMultiplexer> multiplexer,
            Mock<IDistributedLockFactory> lockFactoryMock, int majorVersion,
            Mock<TimeProvider> timeProviderMock, string? prefix = null)
        {
            var multiplexerMock = multiplexer;

            var endpoints = new EndPoint[1];
            endpoints[0] = new DnsEndPoint("localhost", 6379);

            multiplexerMock.Setup(x => x.GetEndPoints(false)).Returns(endpoints);

            var serverMock = new Mock<IServer>();
            serverMock.SetupGet(x => x.Version).Returns(new Version(majorVersion, 0, 0));

            multiplexerMock.Setup(x => x.GetServer(endpoints[0], It.IsAny<object>())).Returns(serverMock.Object);

            multiplexerMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database);

            var cacheOpt = new RedisCacheOptions
            {
                ConnectionOptions = new RedisCacheConnectionOptions(() => Task.FromResult(multiplexerMock.Object))
                {
                    DistributedLockFactory = (_, _) => Task.FromResult(lockFactoryMock.Object),
                },
                Prefix = prefix
            };
            cacheOpt.PostConfigure();
            
            var cacheOptMock = new Mock<IOptions<RedisCacheOptions>>();
            
            cacheOptMock.SetupGet(x => x.Value).Returns(cacheOpt);

            var jsonOpt = GetJsonOptions();
            var jsonOptMock = new Mock<IOptionsMonitor<JsonSerializerOptions>>();
            jsonOptMock.Setup(x => x.Get(RedisExplorer.JsonOptionsName)).Returns(jsonOpt);

            return new RedisExplorer(timeProviderMock.Object, cacheOptMock.Object, jsonOptMock.Object, NullLoggerFactory.Instance.CreateLogger<RedisExplorer>(), NullLoggerFactory.Instance, new ConfigurationOptions());
        }
    }

    public class Get(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldUseCorrectScript()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = distributedCache.Get(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldUseCorrectKeys()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = distributedCache.Get(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(), 
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(), 
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldUseCorrectValues()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = distributedCache.Get(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(), 
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldReturnNullWhenRedisReturnsNil()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
                CommandFlags.None)).Returns(RedisResult.Create(RedisValue.Null));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            var result = distributedCache.Get(fixture.TestKey);
        
            // Assert
            result.Should().BeNull();
        }
        
        [Fact]
        public void ShouldReturnValueWhenRedisReturnsData()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            var result = distributedCache.Get(fixture.TestKey);
        
            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(bytes);
        }
    }

    public class GetAsync(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public async Task ShouldUseCorrectScript()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));

            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            _ = await distributedCache.GetAsync(fixture.TestKey);

            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task ShouldUseCorrectKeys()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));

            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            _ = await distributedCache.GetAsync(fixture.TestKey);

            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldUseCorrectValues()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));

            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            _ = await distributedCache.GetAsync(fixture.TestKey);

            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldReturnNullWhenRedisReturnsNil()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(RedisValue.Null));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            var result = await distributedCache.GetAsync(fixture.TestKey);
        
            // Assert
            result.Should().BeNull();
        }
        
        [Fact]
        public async Task ShouldReturnValueWhenRedisReturnsData()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            var result = await distributedCache.GetAsync(fixture.TestKey);
        
            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(bytes);
        }
    }
    
    public class Set(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldUseCorrectScript()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            distributedCache.Set(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.SetScript), 
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldUseCorrectKeys()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());

            var opt = new DistributedCacheEntryOptions();
        
            // Act
            distributedCache.Set(fixture.TestKey, bytes, opt);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldUseCorrectValues()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());

            var opt = new DistributedCacheEntryOptions();
        
            // Act
            distributedCache.Set(fixture.TestKey, bytes, opt);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 5
                                          && v[0] == LuaScripts.NotPresent
                                          && v[1] == LuaScripts.NotPresent
                                          && v[2] == LuaScripts.NotPresent
                                          && v[3] == bytes
                                          && v[4] == LuaScripts.NotPresent),
                CommandFlags.None), Times.Once);
        }
    }
    
    public class SetAsync(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public async Task ShouldUseCorrectScript()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            await distributedCache.SetAsync(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.SetScript), 
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldUseCorrectKeys()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());

            var opt = new DistributedCacheEntryOptions();
        
            // Act
            await distributedCache.SetAsync(fixture.TestKey, bytes, opt);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldUseCorrectValues()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());

            var opt = new DistributedCacheEntryOptions();
        
            // Act
            await distributedCache.SetAsync(fixture.TestKey, bytes, opt);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 5
                                          && v[0] == LuaScripts.NotPresent
                                          && v[1] == LuaScripts.NotPresent
                                          && v[2] == LuaScripts.NotPresent
                                          && v[3] == bytes
                                          && v[4] == LuaScripts.NotPresent),
                CommandFlags.None), Times.Once);
        }
    }
    
    public class Remove(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldUseCorrectScript()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            distributedCache.Remove(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.RemoveScript),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldUseCorrectKeys()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            distributedCache.Remove(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldUseCorrectValues()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            distributedCache.Remove(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 0),
                CommandFlags.None), Times.Once);
        }
    }
    
    public class RemoveAsync(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public async Task ShouldUseCorrectScript()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            await distributedCache.RemoveAsync(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.RemoveScript),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldUseCorrectKeys()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));

            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            await distributedCache.RemoveAsync(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldUseCorrectValues()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            await distributedCache.RemoveAsync(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 0),
                CommandFlags.None), Times.Once);
        }
    }
    
    public class Refresh(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldUseCorrectScript()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            distributedCache.Refresh(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.RefreshScript),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldUseCorrectKeys()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            distributedCache.Refresh(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldUseCorrectValues()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            distributedCache.Refresh(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 0),
                CommandFlags.None), Times.Once);
        }
    }
    
    public class RefreshAsync(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public async Task ShouldUseCorrectScript()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            await distributedCache.RefreshAsync(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.RefreshScript),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldUseCorrectKeys()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));

            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            await distributedCache.RefreshAsync(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldUseCorrectValues()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var distributedCache = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            await distributedCache.RefreshAsync(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 0),
                CommandFlags.None), Times.Once);
        }
    }
}
