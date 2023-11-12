using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;
using RedLockNet;
using StackExchange.Redis;

namespace RedisExplorer.Unit.Tests;

public class RedisExplorerShould
{
    private JsonSerializerOptions GetJsonOptions() => new();
    private Mock<IDatabase> GetDatabaseMock() => new();
    private Mock<IConnectionMultiplexer> GetMultiplexerMock() => new();
    private Mock<IDistributedLockFactory> GetLockFactoryMock() => new();
    private Mock<TimeProvider> GetTimeProviderMock() => new();
    
    private const string TestString = "testString";
    private const string TestKey = "testKey";
    
    private IRedisExplorer GetTestInstance(IDatabase database, Mock<IConnectionMultiplexer> multiplexer, 
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
            ConnectionMultiplexerFactory = () => Task.FromResult(multiplexerMock.Object),
            DistributedLockFactory = (_,_) => Task.FromResult(lockFactoryMock.Object),
            Prefix = prefix
        };
        var cacheOptMock = new Mock<IOptions<RedisCacheOptions>>();
        cacheOptMock.SetupGet(x => x.Value).Returns(cacheOpt);
        
        var jsonOpt = GetJsonOptions();
        var jsonOptMock = new Mock<IOptionsMonitor<JsonSerializerOptions>>();
        jsonOptMock.Setup(x => x.Get(RedisExplorer.JsonOptionsName)).Returns(jsonOpt);

        var redisExplorerOpt = new RedisExplorerOptions();
        var redisExplorerOptMock = new Mock<IOptions<RedisExplorerOptions>>();
        redisExplorerOptMock.Setup(x => x.Value).Returns(redisExplorerOpt);
        var immutableOpt = new ImmutableRedisExplorerOptions(redisExplorerOptMock.Object);
        
        return new RedisExplorer(timeProviderMock.Object, cacheOptMock.Object, jsonOptMock.Object, immutableOpt);
    }

    [Fact]
    public void Properly_call_internal_factory_create_lock_2_arg()
    {
        // Arrange
        var lockFactoryMock = GetLockFactoryMock();
        var redisExplorer = GetTestInstance(GetDatabaseMock().Object, GetMultiplexerMock(), lockFactoryMock, 6, GetTimeProviderMock());
        
        // Act
        _ = redisExplorer.CreateLock(TestKey, TimeSpan.FromHours(1));
        
        // Assert
        lockFactoryMock.Verify(x => x.CreateLock(TestKey, It.Is<TimeSpan>(y => y.Hours == 1)), Times.Once);
    }
    
    [Fact]
    public async Task Properly_call_internal_factory_create_lock_2_arg_async()
    {
        // Arrange
        var lockFactoryMock = GetLockFactoryMock();
        var redisExplorer = GetTestInstance(GetDatabaseMock().Object, GetMultiplexerMock(), lockFactoryMock, 6, GetTimeProviderMock());
        
        // Act
        _ = await redisExplorer.CreateLockAsync(TestKey, TimeSpan.FromHours(1));
        
        // Assert
        lockFactoryMock.Verify(x => x.CreateLockAsync(TestKey, It.Is<TimeSpan>(y => y.Hours == 1)), Times.Once);
    }
    
    [Fact]
    public void Properly_call_internal_factory_create_lock_4_arg()
    {
        // Arrange
        var lockFactoryMock = GetLockFactoryMock();
        var redisExplorer = GetTestInstance(GetDatabaseMock().Object, GetMultiplexerMock(), lockFactoryMock, 6, GetTimeProviderMock());
        
        // Act
        _ = redisExplorer.CreateLock(TestKey, TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3));
        
        // Assert
        lockFactoryMock.Verify(x => x.CreateLock(TestKey, It.Is<TimeSpan>(y => y.Hours == 1), It.Is<TimeSpan>(y => y.Hours == 2), It.Is<TimeSpan>(y => y.Hours == 3), null), Times.Once);
    }
    
    [Fact]
    public async Task Properly_call_internal_factory_create_lock_4_arg_async()
    {
        // Arrange
        var lockFactoryMock = GetLockFactoryMock();
        var redisExplorer = GetTestInstance(GetDatabaseMock().Object, GetMultiplexerMock(), lockFactoryMock, 6, GetTimeProviderMock());
        
        // Act
        _ = await redisExplorer.CreateLockAsync(TestKey, TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3));
        
        // Assert
        lockFactoryMock.Verify(x => x.CreateLockAsync(TestKey, It.Is<TimeSpan>(y => y.Hours == 1), It.Is<TimeSpan>(y => y.Hours == 2), It.Is<TimeSpan>(y => y.Hours == 3), null), Times.Once);
    }
    
    [Fact]
    public void Returns_prefixed_key()
    {
        // Arrange
        const string prefix = "x:";
        var redisExplorer = GetTestInstance(GetDatabaseMock().Object, GetMultiplexerMock(), GetLockFactoryMock(), 6, GetTimeProviderMock(), prefix);
        
        // Act
        var prefixedKey = redisExplorer.GetPrefixedKey(TestKey);
        
        // Assert
        prefixedKey.Should().Be($"{prefix}{TestKey}");
    }
    
    [Fact]
    public void Properly_call_get()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var bytes = Encoding.UTF8.GetBytes(TestString);

        databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
            CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(TestString)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 6, GetTimeProviderMock());
        
        // Act
        var result = redisExplorer.Get(TestKey);
        
        // Assert
        result.Should().Equal(bytes);
        
        databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
                CommandFlags.None), Times.Once);
    }

    [Fact]
    public void Return_null_from_Get_when_Redis_returns_NIL()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
            CommandFlags.None)).Returns(RedisResult.Create(RedisValue.Null));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 6, GetTimeProviderMock());
        
        // Act
        var result = redisExplorer.Get(TestKey);
        
        // Assert
        result.Should().BeNull();
        
        databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task Properly_call_get_async()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var bytes = Encoding.UTF8.GetBytes(TestString);

        databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
            CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(TestString)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 6, GetTimeProviderMock());
        
        // Act
        var result = await redisExplorer.GetAsync(TestKey);
        
        // Assert
        result.Should().Equal(bytes);
        
        databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
            CommandFlags.None), Times.Once);
    }
    
    [Fact]
    public async Task Return_null_from_GetAsync_when_Redis_returns_NIL()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
            CommandFlags.None)).ReturnsAsync(RedisResult.Create(RedisValue.Null));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(),6, GetTimeProviderMock());
        
        // Act
        var result = await redisExplorer.GetAsync(TestKey);
        
        // Assert
        result.Should().BeNull();
        
        databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
            CommandFlags.None), Times.Once);
    }
    
    [Fact]
    public void Properly_call_get_deserialized()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();

        var testObj = new TestObj(TestString);

        var serialized = JsonSerializer.Serialize(testObj, GetJsonOptions());

        databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
            CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(serialized)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(),6, GetTimeProviderMock());
        
        // Act
        var result = redisExplorer.GetDeserialized<TestObj>(TestKey);
        
        // Assert
        result.Should().NotBeNull();
        result!.Test.Should().Be(TestString);
        result.Should().Be(testObj);
        
        databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
                CommandFlags.None), Times.Once);
    }
    
    [Fact]
    public void Return_null_from_Get_deserialized_when_Redis_returns_NIL()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();

        databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
            CommandFlags.None)).Returns(RedisResult.Create(RedisValue.Null));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(),6, GetTimeProviderMock());
        
        // Act
        var result = redisExplorer.GetDeserialized<TestObj>(TestKey);
        
        // Assert
        result.Should().BeNull();
        
        databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
            CommandFlags.None), Times.Once);
    }
    
    [Fact]
    public async Task Properly_call_get_deserialized_async()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var testObj = new TestObj(TestString);

        var serialized = JsonSerializer.Serialize(testObj, GetJsonOptions());

        databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
            CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(serialized)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(),6, GetTimeProviderMock());
        
        // Act
        var result = await redisExplorer.GetDeserializedAsync<TestObj>(TestKey);
        
        // Assert
        result.Should().NotBeNull();
        result!.Test.Should().Be(TestString);
        result.Should().Be(testObj);
        
        databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
            CommandFlags.None), Times.Once);
    }
    
    [Fact]
    public async Task Return_null_from_GetAsync_deserialized_when_Redis_returns_NIL()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();

        databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg),
            CommandFlags.None)).ReturnsAsync(RedisResult.Create(RedisValue.Null));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(),6, GetTimeProviderMock());
        
        // Act
        var result = await redisExplorer.GetDeserializedAsync<TestObj>(TestKey);
        
        // Assert
        result.Should().BeNull();
        
        databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.GetAndRefreshScript), 
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 1 && v[0] == LuaScripts.ReturnDataArg), 
            CommandFlags.None), Times.Once);
    }
    
    [Fact]
    public void Properly_call_Set_pre_extended()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var bytes = Encoding.UTF8.GetBytes(TestString);

        databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.SetScriptPreExtendedSetCommand),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4 
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == bytes),
            CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(TestString)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 2, GetTimeProviderMock());

        var opt = new DistributedCacheEntryOptions();
        
        // Act
        redisExplorer.Set(TestKey, bytes, opt);
        
        // Assert
        databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.SetScriptPreExtendedSetCommand),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == bytes),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task Properly_call_SetAsync_pre_extended()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var bytes = Encoding.UTF8.GetBytes(TestString);

        databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.SetScriptPreExtendedSetCommand),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4 
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == bytes),
            CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(TestString)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 2, GetTimeProviderMock());

        var opt = new DistributedCacheEntryOptions();
        
        // Act
        await redisExplorer.SetAsync(TestKey, bytes, opt);
        
        // Assert
        databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.SetScriptPreExtendedSetCommand),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == bytes),
            CommandFlags.None), Times.Once);
    }
    
    [Fact]
    public void Properly_call_Set()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var bytes = Encoding.UTF8.GetBytes(TestString);

        databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.SetScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4 
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == bytes),
            CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(TestString)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 5, GetTimeProviderMock());

        var opt = new DistributedCacheEntryOptions();
        
        // Act
        redisExplorer.Set(TestKey, bytes, opt);
        
        // Assert
        databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.SetScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == bytes),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task Properly_call_SetAsync()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var bytes = Encoding.UTF8.GetBytes(TestString);

        databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.SetScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4 
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == bytes),
            CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(TestString)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 5, GetTimeProviderMock());

        var opt = new DistributedCacheEntryOptions();
        
        // Act
        await redisExplorer.SetAsync(TestKey, bytes, opt);
        
        // Assert
        databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.SetScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == bytes),
            CommandFlags.None), Times.Once);
    }
    
    [Fact]
    public void Properly_call_SetSerialized_pre_extended()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var testObj = new TestObj(TestString);

        var serialized = JsonSerializer.SerializeToUtf8Bytes(testObj, GetJsonOptions());

        databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.SetScriptPreExtendedSetCommand),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4 
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == serialized),
            CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(TestString)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 2, GetTimeProviderMock());

        var opt = new DistributedCacheEntryOptions();
        
        // Act
        redisExplorer.SetSerialized(TestKey, testObj, opt);
        
        // Assert
        databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.SetScriptPreExtendedSetCommand),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == serialized),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task Properly_call_SetSerializedAsync_pre_extended()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        var testObj = new TestObj(TestString);

        var serialized = JsonSerializer.SerializeToUtf8Bytes(testObj, GetJsonOptions());

        databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.SetScriptPreExtendedSetCommand),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4 
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == serialized),
            CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(TestString)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 2, GetTimeProviderMock());

        var opt = new DistributedCacheEntryOptions();
        
        // Act
        await redisExplorer.SetSerializedAsync(TestKey, testObj, opt);
        
        // Assert
        databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.SetScriptPreExtendedSetCommand),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 4
                                      && v[0] == LuaScripts.NotPresent
                                      && v[1] == LuaScripts.NotPresent
                                      && v[2] == LuaScripts.NotPresent
                                      && v[3] == serialized),
            CommandFlags.None), Times.Once);
    }
    
     [Fact]
    public void Properly_call_Refresh()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        databaseMock.Setup(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.RefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 0),
            CommandFlags.None)).Returns(RedisResult.Create(new RedisValue(LuaScripts.SuccessfulScriptNoDataReturnedValue)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 5, GetTimeProviderMock());
        
        // Act
        redisExplorer.Refresh(TestKey);
        
        // Assert
        databaseMock.Verify(x => x.ScriptEvaluate(It.Is<string>(s => s == LuaScripts.RefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 0),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task Properly_call_RefreshAsync()
    {
        // Arrange
        var databaseMock = GetDatabaseMock();
        
        databaseMock.Setup(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.RefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 0),
            CommandFlags.None)).ReturnsAsync(RedisResult.Create(new RedisValue(LuaScripts.SuccessfulScriptNoDataReturnedValue)));
        
        var redisExplorer = GetTestInstance(databaseMock.Object, GetMultiplexerMock(), GetLockFactoryMock(), 5, GetTimeProviderMock());
        
        // Act
        await redisExplorer.RefreshAsync(TestKey);
        
        // Assert
        databaseMock.Verify(x => x.ScriptEvaluateAsync(It.Is<string>(s => s == LuaScripts.RefreshScript),
            It.Is<RedisKey[]?>(k => k!.Length == 1 && k[0] == TestKey),
            It.Is<RedisValue[]?>(v => v!.Length == 0),
            CommandFlags.None), Times.Once);
    }

    private sealed record TestObj(string Test);
}
