using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedLockNet;
using StackExchange.Redis;

namespace RedisExplorer.Tests.Unit;

[UsedImplicitly]
public class RedisExplorerTests
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

        public IRedisExplorer GetTestInstance(IDatabase database, Mock<IConnectionMultiplexer> multiplexer,
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


    public class CreateLock2Arg(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldProperlyCallInternalFactory()
        {
            // Arrange
            var lockFactoryMock = fixture.GetLockFactoryMock();
            var redisExplorer = fixture.GetTestInstance(fixture.GetDatabaseMock().Object, fixture.GetMultiplexerMock(), lockFactoryMock, 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = redisExplorer.CreateLock(fixture.TestKey, TimeSpan.FromHours(1));
        
            // Assert
            lockFactoryMock.Verify(x => x.CreateLock(fixture.TestKey, It.Is<TimeSpan>(y => y.Hours == 1)), Times.Once);
        }

    }

    public class CreateLockAsync2Arg(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public async Task ShouldProperlyCallInternalFactory()
        {
            // Arrange
            var lockFactoryMock = fixture.GetLockFactoryMock();
            var redisExplorer = fixture.GetTestInstance(fixture.GetDatabaseMock().Object, fixture.GetMultiplexerMock(), lockFactoryMock, 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = await redisExplorer.CreateLockAsync(fixture.TestKey, TimeSpan.FromHours(1));
        
            // Assert
            lockFactoryMock.Verify(x => x.CreateLockAsync(fixture.TestKey, It.Is<TimeSpan>(y => y.Hours == 1)), Times.Once);
        }
    }

    public class CreateLock4Arg(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldProperlyCallInternalFactory()
        {
            // Arrange
            var lockFactoryMock = fixture.GetLockFactoryMock();
            var redisExplorer = fixture.GetTestInstance(fixture.GetDatabaseMock().Object, fixture.GetMultiplexerMock(), lockFactoryMock, 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = redisExplorer.CreateLock(fixture.TestKey, TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3));
        
            // Assert
            lockFactoryMock.Verify(x => x.CreateLock(fixture.TestKey, It.Is<TimeSpan>(y => y.Hours == 1), It.Is<TimeSpan>(y => y.Hours == 2), It.Is<TimeSpan>(y => y.Hours == 3), CancellationToken.None), Times.Once);
        }
    }
    
    public class CreateLockAsync4Arg(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public async Task ShouldProperlyCallInternalFactory()
        {
            // Arrange
            var lockFactoryMock = fixture.GetLockFactoryMock();
            var redisExplorer = fixture.GetTestInstance(fixture.GetDatabaseMock().Object, fixture.GetMultiplexerMock(), lockFactoryMock, 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = await redisExplorer.CreateLockAsync(fixture.TestKey, TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3));
        
            // Assert
            lockFactoryMock.Verify(x => x.CreateLockAsync(fixture.TestKey, It.Is<TimeSpan>(y => y.Hours == 1), It.Is<TimeSpan>(y => y.Hours == 2), It.Is<TimeSpan>(y => y.Hours == 3), CancellationToken.None), Times.Once);
        }
    }

    public class GetPrefixedKey(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldReturnCorrectKey()
        {
            // Arrange
            const string prefix = "x:";
            var redisExplorer = fixture.GetTestInstance(fixture.GetDatabaseMock().Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock(), prefix);
        
            // Act
            var prefixedKey = redisExplorer.GetPrefixedKey(fixture.TestKey);
        
            // Assert
            prefixedKey.Should().Be($"{prefix}{fixture.TestKey}");
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
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = redisExplorer.Get(fixture.TestKey);
        
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
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = redisExplorer.Get(fixture.TestKey);
        
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
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            _ = redisExplorer.Get(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(), 
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.AlsoRefreshArg), 
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldReturnExplorerResultWithNotFoundFlagWhenRedisReturnsNil()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.AlsoRefreshArg),
                CommandFlags.None)).Returns(RedisResult.Create(RedisValue.Null));
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            var result = redisExplorer.Get(fixture.TestKey);
        
            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyNotFound).And
                .NotHaveFlag(ExplorerResultFlags.Success);
        }
        
        [Fact]
        public void ShouldReturnDefinedExplorerResultWhenRedisReturnsData()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.AlsoRefreshArg),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            var result = redisExplorer.Get(fixture.TestKey);
        
            // Assert
            result.IsDefined(out var data).Should().BeTrue();
            data.Should().BeEquivalentTo(bytes);
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

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            _ = await redisExplorer.GetAsync(fixture.TestKey);

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

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            _ = await redisExplorer.GetAsync(fixture.TestKey);

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

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            _ = await redisExplorer.GetAsync(fixture.TestKey);

            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.AlsoRefreshArg),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldReturnExplorerResultWithNotFoundFlagWhenRedisReturnsNil()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.AlsoRefreshArg),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(RedisValue.Null));
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            var result = await redisExplorer.GetAsync(fixture.TestKey);
        
            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyNotFound).And
                .NotHaveFlag(ExplorerResultFlags.Success);
        }
        
        [Fact]
        public async Task ShouldReturnDefinedExplorerResultWhenRedisReturnsData()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
            
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.AlsoRefreshArg),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            var result = await redisExplorer.GetAsync(fixture.TestKey);
        
            // Assert
            result.IsDefined(out var data).Should().BeTrue();
            data.Should().BeEquivalentTo(bytes);
        }
    }
    
    public class GetDeserialized(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldDeserializeAValidResult()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            var testObj = new TestObj(fixture.TestString);

            var serialized = JsonSerializer.Serialize(testObj, fixture.GetJsonOptions());

            databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.AlsoRefreshArg),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(serialized)));
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(),6, fixture.GetTimeProviderMock());
        
            // Act
            var result = redisExplorer.Get<TestObj>(fixture.TestKey);
        
            // Assert
            var serializedBytes = Encoding.UTF8.GetBytes(serialized);
            var valueBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result.Value));

            valueBytes.Should().BeEquivalentTo(serializedBytes);
        }
    }

    public class GetAsyncDeserialized(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public async Task ShouldDeserializeAValidResult()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            var testObj = new TestObj(fixture.TestString);

            var serialized = JsonSerializer.Serialize(testObj, fixture.GetJsonOptions());

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.AlsoRefreshArg),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(serialized)));

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            var result = await redisExplorer.GetAsync<TestObj>(fixture.TestKey);
            
            // Assert
            var serializedBytes = Encoding.UTF8.GetBytes(serialized);
            var valueBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result.Value));

            valueBytes.Should().BeEquivalentTo(serializedBytes);
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

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            redisExplorer.SetAsync(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));

            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.SetScript),
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
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            redisExplorer.Set(fixture.TestKey, bytes);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ShouldUseCorrectValues(bool shouldOverwrite)
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());

            Action<SetOperationOptions> opt = x => x.WithoutKeyOverwriting(shouldOverwrite);
        
            // Act
            redisExplorer.Set(fixture.TestKey, bytes, opt);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 5
                                          && v[0] == LuaScripts.NotPresent
                                          && v[1] == LuaScripts.NotPresent
                                          && v[2] == LuaScripts.NotPresent
                                          && v[3] == bytes
                                          && v[4] == (!shouldOverwrite ? LuaScripts.WithoutKeyOverwriteArg : LuaScripts.NotPresent)),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldReturnResultWithKeyOverwrittenFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(LuaScripts.SetOverwrittenReturn)));

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            var result = redisExplorer.Set(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));

            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyOverwritten);
        }
        
        [Fact]
        public void ShouldReturnResultWithOnlySuccessFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(LuaScripts.SuccessReturn)));

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            var result = redisExplorer.Set(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));

            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.Success);
            result.Flags.Should().NotHaveFlag(ExplorerResultFlags.KeyOverwritten);
            result.Flags.Should().NotHaveFlag(ExplorerResultFlags.KeyCollision);
        }
        
        [Fact]
        public void ShouldReturnResultWithKeyCollisionFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(LuaScripts.SetCollisionReturn)));

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            var result = redisExplorer.Set(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));

            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyCollision);
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
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());
        
            // Act
            await redisExplorer.SetAsync(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));
        
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
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            await redisExplorer.SetAsync(fixture.TestKey, bytes);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == fixture.TestKey),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None), Times.Once);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ShouldUseCorrectValues(bool shouldOverwrite)
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            var bytes = Encoding.UTF8.GetBytes(fixture.TestString);

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
        
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());

            Action<SetOperationOptions> opt = x => x.WithoutKeyOverwriting(shouldOverwrite);
        
            // Act
            await redisExplorer.SetAsync(fixture.TestKey, bytes, opt);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 5
                                          && v[0] == LuaScripts.NotPresent
                                          && v[1] == LuaScripts.NotPresent
                                          && v[2] == LuaScripts.NotPresent
                                          && v[3] == bytes
                                          && v[4] == (!shouldOverwrite ? LuaScripts.WithoutKeyOverwriteArg : LuaScripts.NotPresent)),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldReturnResultWithKeyOverwrittenFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(LuaScripts.SetOverwrittenReturn)));

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            var result = await redisExplorer.SetAsync(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));

            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyOverwritten);
        }
        
        [Fact]
        public async Task ShouldReturnResultWithOnlySuccessFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(LuaScripts.SuccessReturn)));

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            var result = await redisExplorer.SetAsync(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));

            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.Success);
            result.Flags.Should().NotHaveFlag(ExplorerResultFlags.KeyOverwritten);
            result.Flags.Should().NotHaveFlag(ExplorerResultFlags.KeyCollision);
        }
        
        [Fact]
        public async Task ShouldReturnResultWithKeyCollisionFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();

            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(LuaScripts.SetCollisionReturn)));

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(),
                fixture.GetLockFactoryMock(), 6, fixture.GetTimeProviderMock());

            // Act
            var result = await redisExplorer.SetAsync(fixture.TestKey, Encoding.UTF8.GetBytes(fixture.TestKey));

            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyCollision);
        }
    }

    public class SetSerialized(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public void ShouldCorrectlySerialize()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            var testObj = new TestObj(fixture.TestString);

            var serialized = JsonSerializer.SerializeToUtf8Bytes(testObj, fixture.GetJsonOptions());
            
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 2, fixture.GetTimeProviderMock());
            
            // Act
            redisExplorer.Set(fixture.TestKey, testObj);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v![3] == serialized),
                CommandFlags.None), Times.Once);
        }
    }
    
    public class SetAsyncSerialized(Fixture fixture) : IClassFixture<Fixture>
    {
        [Fact]
        public async Task ShouldCorrectlySerialize()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            var testObj = new TestObj(fixture.TestString);

            var serialized = JsonSerializer.SerializeToUtf8Bytes(testObj, fixture.GetJsonOptions());
            
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(fixture.TestString)));
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 2, fixture.GetTimeProviderMock());
            
            // Act
            await redisExplorer.SetAsync(fixture.TestKey, testObj);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v![3] == serialized),
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            redisExplorer.Remove(fixture.TestKey);
        
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            redisExplorer.Remove(fixture.TestKey);
        
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            redisExplorer.Remove(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 0),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldReturnResultWithKeyNotFoundFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(RedisValue.Null));
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            var result = redisExplorer.Remove(fixture.TestKey);
        
            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyNotFound);
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            await redisExplorer.RemoveAsync(fixture.TestKey);
        
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

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            await redisExplorer.RemoveAsync(fixture.TestKey);
        
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            await redisExplorer.RemoveAsync(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 0),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldReturnResultWithKeyNotFoundFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(RedisValue.Null));
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            var result = await redisExplorer.RemoveAsync(fixture.TestKey);
        
            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyNotFound);
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            redisExplorer.Refresh(fixture.TestKey);
        
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            redisExplorer.Refresh(fixture.TestKey);
        
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            redisExplorer.Refresh(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 0),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public void ShouldReturnResultWithKeyNotFoundFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(RedisValue.Null));
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            var result = redisExplorer.Refresh(fixture.TestKey);
        
            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyNotFound);
        }
        
        [Fact]
        public void ShouldReturnResultWithNoSlidingExpirationFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(LuaScripts.RefreshNoSlidingExpirationReturn)));
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            var result = redisExplorer.Refresh(fixture.TestKey);
        
            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.NoSlidingExpiration);
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            await redisExplorer.RefreshAsync(fixture.TestKey);
        
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

            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
            
            // Act
            await redisExplorer.RefreshAsync(fixture.TestKey);
        
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
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            await redisExplorer.RefreshAsync(fixture.TestKey);
        
            // Assert
            databaseMock.Verify(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.Is<RedisValue[]?>(v => v!.Length == 0),
                CommandFlags.None), Times.Once);
        }
        
        [Fact]
        public async Task ShouldReturnResultWithKeyNotFoundFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(RedisValue.Null));
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            var result = await redisExplorer.RefreshAsync(fixture.TestKey);
        
            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.KeyNotFound);
        }
        
        [Fact]
        public async Task ShouldReturnResultWithNoSlidingExpirationFlag()
        {
            // Arrange
            var databaseMock = fixture.GetDatabaseMock();
        
            databaseMock.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(LuaScripts.RefreshNoSlidingExpirationReturn)));
            
            var redisExplorer = fixture.GetTestInstance(databaseMock.Object, fixture.GetMultiplexerMock(), fixture.GetLockFactoryMock(), 5, fixture.GetTimeProviderMock());
        
            // Act
            var result = await redisExplorer.RefreshAsync(fixture.TestKey);
        
            // Assert
            result.Flags.Should().HaveFlag(ExplorerResultFlags.NoSlidingExpiration);
        }
    }

    private sealed record TestObj(string Test);
}
