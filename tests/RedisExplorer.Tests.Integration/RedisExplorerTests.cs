using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace RedisExplorer.Tests.Integration;

[UsedImplicitly]
public sealed class Fixture : IDisposable, IAsyncDisposable
{
    public string TestString => "testString";
    public string GetKey() => Guid.NewGuid().ToString();

    public TimeSpan Delay = TimeSpan.FromSeconds(1);
    public TimeSpan AcceptedDelta = TimeSpan.FromSeconds(1);

    private readonly ConnectionMultiplexer _multiplexer;
    private readonly RedisContainer _container;

    private const string RedisImage = "redis/redis-stack:latest";
    private const string RedisContainerName = "redis-explorer-test";

    public Fixture()
    {
        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Creating Redis container...");

        _container = new RedisBuilder()
            .WithImage(RedisImage)
            .WithName(RedisContainerName)
            .WithExposedPort(RedisBuilder.RedisPort)
            .Build();

        _container.StartAsync().GetAwaiter().GetResult();

        _multiplexer = ConnectionMultiplexer.Connect(_container.GetConnectionString());

        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Redis container created");
    }

    public IRedisExplorer GetTestInstance(TimeProvider? timeProvider = null)
    {
        var cacheOpt = new RedisCacheOptions
        {
            ConnectionOptions =
                new RedisCacheConnectionOptions(() => Task.FromResult((IConnectionMultiplexer)_multiplexer))
        };
        cacheOpt.PostConfigure();
        var cacheOptMock = new Mock<IOptions<RedisCacheOptions>>();
        cacheOptMock.SetupGet(x => x.Value).Returns(cacheOpt);

        var jsonOpt = new JsonSerializerOptions();
        var jsonOptMock = new Mock<IOptionsMonitor<JsonSerializerOptions>>();
        jsonOptMock.Setup(x => x.Get(RedisExplorer.JsonOptionsName)).Returns(jsonOpt);

        return new RedisExplorer(timeProvider ?? TimeProvider.System, cacheOptMock.Object, jsonOptMock.Object);
    }

    public void Dispose()
    {
        _multiplexer.Dispose();
        _container.StopAsync().GetAwaiter().GetResult();
        _container.DisposeAsync().AsTask().GetAwaiter().GetResult();

        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Test containers pruned");
    }

    public async ValueTask DisposeAsync()
    {
        await _multiplexer.DisposeAsync();
        await _container.StopAsync();
        await _container.DisposeAsync();

        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Test containers pruned");
    }
}

[UsedImplicitly]
[CollectionDefinition("IntegrationTests")]
public class RedisExplorerTests : ICollectionFixture<Fixture>
{
    [Collection("IntegrationTests")]
    public class Set
    {
        private readonly Fixture _fixture;

        public Set(Fixture fixture)
        {
            _fixture = fixture;
        }

        public static object[][] ExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2)) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2))} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(1))} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(10)), SlidingExpiration = TimeSpan.FromMinutes(1)} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) } }
            };

        [Theory]
        [MemberData(nameof(ExpirationTestCases))]
        public void ShouldSetCorrectExpirationWhenOptionsNotEmpty(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var now = TimeProvider.System.GetUtcNow();

            var key = _fixture.GetKey();
            
            // Act
            testObj.SetString(key, _fixture.TestString, options);
            
            // Assert
            var expiration = testObj.GetDatabase().KeyExpireTime(key);
            
            expiration.Should().NotBeNull();
            expiration!.Value.Should().BeAfter(now.DateTime);
            
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            
            expiration.Should().BeCloseTo(expAsDt, _fixture.AcceptedDelta);
        }
        
        [Fact]
        public void ShouldNotSetExpirationWhenOptionsEmpty()
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var now = TimeProvider.System.GetUtcNow();

            var key = _fixture.GetKey();
            
            // Act
            testObj.SetString(key, _fixture.TestString);
            
            // Assert
            var expiration = testObj.GetDatabase().KeyExpireTime(key);

            expiration.Should().BeNull();
        }
    }
    
    [Collection("IntegrationTests")]
    public class SetAsync
    {
        private readonly Fixture _fixture;

        public SetAsync(Fixture fixture)
        {
            _fixture = fixture;
        }

        public static object[][] ExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2)) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2))} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(1))} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(10)), SlidingExpiration = TimeSpan.FromMinutes(1)} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) } }
            };

        [Theory]
        [MemberData(nameof(ExpirationTestCases))]
        public async Task ShouldSetCorrectExpirationWhenOptionsNotEmpty(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var now = TimeProvider.System.GetUtcNow();

            var key = _fixture.GetKey();
            
            // Act
            await testObj.SetStringAsync(key, _fixture.TestString, options);
            
            // Assert
            var expiration = await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);
            
            expiration.Should().NotBeNull();
            expiration!.Value.Should().BeAfter(now.DateTime);
            
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            
            expiration.Should().BeCloseTo(expAsDt, _fixture.AcceptedDelta);
        }
        
        [Fact]
        public async Task ShouldNotSetExpirationWhenOptionsEmpty()
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var now = TimeProvider.System.GetUtcNow();

            var key = _fixture.GetKey();
            
            // Act
            await testObj.SetStringAsync(key, _fixture.TestString);
            
            // Assert
            var expiration = await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);

            expiration.Should().BeNull();
        }
    }
    
    [Collection("IntegrationTests")]
    public class Get
    {
        private readonly Fixture _fixture;

        public Get(Fixture fixture)
        {
            _fixture = fixture;
        }

        public static object[][] NoSlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2)) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2))} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(1))} },
            };

        [Theory]
        [MemberData(nameof(NoSlidingExpirationTestCases))]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldNotRefreshExpirationWhenNoSlidingExpiration(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var key = _fixture.GetKey();
            
            // Act
            testObj.SetString(key, _fixture.TestString, options);
            
            var preGetExpiration = testObj.GetDatabase().KeyExpireTime(key);

            await Task.Delay(_fixture.Delay);
            
            _ = testObj.GetString(key);
            
            var postGetExpiration = testObj.GetDatabase().KeyExpireTime(key);
            
            // Assert
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().Be(postGetExpiration);
        }
        
        public static object[][] SlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(10)), SlidingExpiration = TimeSpan.FromMinutes(1)} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) } }
            };
        
        [Theory]
        [MemberData(nameof(SlidingExpirationTestCases))]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldRefreshExpirationWhenSlidingExpirationNonNull(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var key = _fixture.GetKey();
            var now = TimeProvider.System.GetUtcNow();
            
            // Act
            testObj.SetString(key, _fixture.TestString, options);
            
            var preGetExpiration = testObj.GetDatabase().KeyExpireTime(key);

            await Task.Delay(_fixture.Delay);
            
            _ = testObj.GetString(key);
            
            var postGetExpiration = testObj.GetDatabase().KeyExpireTime(key);
            
            // Assert
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            var preExpAsDt = expAsDt;
            var postExpAsDt = expAsDt.Add(_fixture.Delay);
            
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().NotBe(postGetExpiration);
            postGetExpiration!.Value.Should().BeAfter(preGetExpiration!.Value);
            postGetExpiration.Should().BeCloseTo(postExpAsDt, _fixture.AcceptedDelta);
            preGetExpiration.Should().BeCloseTo(preExpAsDt, _fixture.AcceptedDelta);
        }
    }
    
    [Collection("IntegrationTests")]
    public class GetAsync
    {
        private readonly Fixture _fixture;

        public GetAsync(Fixture fixture)
        {
            _fixture = fixture;
        }

        public static object[][] NoSlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2)) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2))} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(1))} },
            };

        [Theory]
        [MemberData(nameof(NoSlidingExpirationTestCases))]
        public async Task ShouldNotRefreshExpirationWhenNoSlidingExpiration(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var key = _fixture.GetKey();
            
            // Act
            await testObj.SetStringAsync(key, _fixture.TestString, options);
            
            var preGetExpiration = await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);

            await Task.Delay(_fixture.Delay);
            
            _ = await testObj.GetStringAsync(key);
            
            var postGetExpiration = await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);
            
            // Assert
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().Be(postGetExpiration);
        }
        
        public static object[][] SlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(10)), SlidingExpiration = TimeSpan.FromMinutes(1)} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) } }
            };
        
        [Theory]
        [MemberData(nameof(SlidingExpirationTestCases))]
        public async Task ShouldRefreshExpirationWhenSlidingExpirationNonNull(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var key = _fixture.GetKey();
            var now = TimeProvider.System.GetUtcNow();
            
            // Act
            await testObj.SetStringAsync(key, _fixture.TestString, options);
            
            var preGetExpiration =  await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);

            await Task.Delay(_fixture.Delay);
            
            _ = await testObj.GetStringAsync(key);
            
            var postGetExpiration = await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);
            
            // Assert
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            var preExpAsDt = expAsDt;
            var postExpAsDt = expAsDt.Add(_fixture.Delay);
            
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().NotBe(postGetExpiration);
            postGetExpiration!.Value.Should().BeAfter(preGetExpiration!.Value);
            postGetExpiration.Should().BeCloseTo(postExpAsDt, _fixture.AcceptedDelta);
            preGetExpiration.Should().BeCloseTo(preExpAsDt, _fixture.AcceptedDelta);
        }
    }
    
    [Collection("IntegrationTests")]
    public class Refresh
    {
        private readonly Fixture _fixture;

        public Refresh(Fixture fixture)
        {
            _fixture = fixture;
        }

        public static object[][] NoSlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2)) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2))} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(1))} },
            };

        [Theory]
        [MemberData(nameof(NoSlidingExpirationTestCases))]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldNotRefreshExpirationWhenNoSlidingExpiration(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var key = _fixture.GetKey();
            
            // Act
            testObj.SetString(key, _fixture.TestString, options);
            
            var preGetExpiration = testObj.GetDatabase().KeyExpireTime(key);

            await Task.Delay(_fixture.Delay);
            
            testObj.Refresh(key);
            
            var postGetExpiration = testObj.GetDatabase().KeyExpireTime(key);
            
            // Assert
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().Be(postGetExpiration);
        }
        
        public static object[][] SlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(10)), SlidingExpiration = TimeSpan.FromMinutes(1)} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) } }
            };
        
        [Theory]
        [MemberData(nameof(SlidingExpirationTestCases))]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldRefreshExpirationWhenSlidingExpirationNonNull(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var key = _fixture.GetKey();
            var now = TimeProvider.System.GetUtcNow();
            
            // Act
            testObj.SetString(key, _fixture.TestString, options);
            
            var preGetExpiration = testObj.GetDatabase().KeyExpireTime(key);

            await Task.Delay(_fixture.Delay);
            
            testObj.Refresh(key);
            
            var postGetExpiration = testObj.GetDatabase().KeyExpireTime(key);
            
            // Assert
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            var preExpAsDt = expAsDt;
            var postExpAsDt = expAsDt.Add(_fixture.Delay);
            
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().NotBe(postGetExpiration);
            postGetExpiration!.Value.Should().BeAfter(preGetExpiration!.Value);
            postGetExpiration.Should().BeCloseTo(postExpAsDt, _fixture.AcceptedDelta);
            preGetExpiration.Should().BeCloseTo(preExpAsDt, _fixture.AcceptedDelta);
        }
    }
    
    [Collection("IntegrationTests")]
    public class RefreshAsync
    {
        private readonly Fixture _fixture;

        public RefreshAsync(Fixture fixture)
        {
            _fixture = fixture;
        }

        public static object[][] NoSlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2)) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(2))} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(1))} },
            };

        [Theory]
        [MemberData(nameof(NoSlidingExpirationTestCases))]
        public async Task ShouldNotRefreshExpirationWhenNoSlidingExpiration(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var key = _fixture.GetKey();
            
            // Act
            await testObj.SetStringAsync(key, _fixture.TestString, options);
            
            var preGetExpiration = await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);

            await Task.Delay(_fixture.Delay);
            
            await testObj.RefreshAsync(key);
            
            var postGetExpiration = await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);
            
            // Assert
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().Be(postGetExpiration);
        }
        
        public static object[][] SlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(10)), SlidingExpiration = TimeSpan.FromMinutes(1)} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) } }
            };
        
        [Theory]
        [MemberData(nameof(SlidingExpirationTestCases))]
        public async Task ShouldRefreshExpirationWhenSlidingExpirationNonNull(DistributedCacheEntryOptions options)
        {
            // Arrange
            var testObj = _fixture.GetTestInstance();
            var key = _fixture.GetKey();
            var now = TimeProvider.System.GetUtcNow();
            
            // Act
            await testObj.SetStringAsync(key, _fixture.TestString, options);
            
            var preGetExpiration =  await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);

            await Task.Delay(_fixture.Delay);
            
            await testObj.RefreshAsync(key);
            
            var postGetExpiration = await (await testObj.GetDatabaseAsync()).KeyExpireTimeAsync(key);
            
            // Assert
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            var preExpAsDt = expAsDt;
            var postExpAsDt = expAsDt.Add(_fixture.Delay);
            
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().NotBe(postGetExpiration);
            postGetExpiration!.Value.Should().BeAfter(preGetExpiration!.Value);
            postGetExpiration.Should().BeCloseTo(postExpAsDt, _fixture.AcceptedDelta);
            preGetExpiration.Should().BeCloseTo(preExpAsDt, _fixture.AcceptedDelta);
        }
    }

    private sealed record TestObj(string Test);
}
