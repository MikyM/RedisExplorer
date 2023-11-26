﻿using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace RedisExplorer.Tests.Integration;

public abstract class Fixture : IDisposable, IAsyncDisposable
{
    public string TestString => "testString";
    public string GetKey() => Guid.NewGuid().ToString();
    public TimeSpan Delay = TimeSpan.FromSeconds(1);
    public TimeSpan AcceptedDelta = TimeSpan.FromSeconds(1);
    public const string RedisImage = "redis/redis-stack:latest";
    public void Initialize() => InitializeAsync().GetAwaiter().GetResult();
    public abstract Task InitializeAsync();
    public abstract Task TeardownAsync();
    public abstract IRedisExplorer GetTestInstance(TimeProvider? timeProvider = null);
    
    public void Dispose()
    {
        TeardownAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await TeardownAsync();
    }
}

[UsedImplicitly]
public class SingleInstanceFixture : Fixture
{
    private bool _initialized;
    private ConnectionMultiplexer? _multiplexer;
    private RedisContainer? _container;
    private const string RedisContainerName = "redis-explorer-test-redis-single";
    
    public override async Task InitializeAsync()
    {
        if (_initialized)
            return;
        
        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Creating Redis container...");

        _container = new RedisBuilder()
            .WithImage(RedisImage)
            .WithName(RedisContainerName)
            .WithPortBinding(RedisBuilder.RedisPort, RedisBuilder.RedisPort)
            .Build();

        await _container.StartAsync();

        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());

        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Redis container created");

        _initialized = true;
    }

    public override async Task TeardownAsync()
    {
        if (_multiplexer != null)
        {
            await _multiplexer.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Test containers pruned");
    }

    public override IRedisExplorer GetTestInstance(TimeProvider? timeProvider = null)
    {
        if (_initialized == false)
        {
            throw new InvalidOperationException("Fixture not initialized");
        }
        
        var cacheOpt = new RedisCacheOptions
        {
            ConnectionOptions =
                new RedisCacheConnectionOptions(() => Task.FromResult((IConnectionMultiplexer)_multiplexer!))
        };
        cacheOpt.PostConfigure();
        var cacheOptMock = new Mock<IOptions<RedisCacheOptions>>();
        cacheOptMock.SetupGet(x => x.Value).Returns(cacheOpt);

        var jsonOpt = new JsonSerializerOptions();
        var jsonOptMock = new Mock<IOptionsMonitor<JsonSerializerOptions>>();
        jsonOptMock.Setup(x => x.Get(RedisExplorer.JsonOptionsName)).Returns(jsonOpt);

        return new RedisExplorer(timeProvider ?? TimeProvider.System, cacheOptMock.Object, jsonOptMock.Object);
    }
}

[UsedImplicitly]
public sealed class ClusterFixture : IDisposable, IAsyncDisposable
{
    public string TestString => "testString";
    public string GetKey() => Guid.NewGuid().ToString();

    public TimeSpan Delay = TimeSpan.FromSeconds(1);
    public TimeSpan AcceptedDelta = TimeSpan.FromSeconds(1);

    private readonly ConnectionMultiplexer _multiplexer;
    private readonly RedisContainer _container;

    private const string RedisImage = "redis/redis-stack:latest";
    private const string RedisContainerName = "redis-explorer-test-redis";

    public ClusterFixture()
    {
        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Creating Redis container...");

        _container = new RedisBuilder()
            .WithImage(RedisImage)
            .WithName(RedisContainerName)
            .WithPortBinding(RedisBuilder.RedisPort, RedisBuilder.RedisPort)
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
public sealed class EnvoyProxyClusterFixture : IDisposable, IAsyncDisposable
{
    public string TestString => "testString";
    public string GetKey() => Guid.NewGuid().ToString();

    public TimeSpan Delay = TimeSpan.FromSeconds(1);
    public TimeSpan AcceptedDelta = TimeSpan.FromSeconds(1);

    private readonly ConnectionMultiplexer _multiplexer;
    private readonly RedisContainer _container;
    private readonly IContainer _envoyContainer;

    private const string RedisImage = "redis/redis-stack:latest";
    private const string RedisContainerName = "redis-explorer-envoyproxy-test-redis";
    private const string EnovyImage = "envoyproxy/envoy:v1.28-latest";
    private const string EnovyContainerName = "redis-explorer-envoyproxy-test-envoy";

    public const int RedisContainerPort = 6377;
    public const int EnvoyContainerPort = 6378;

    public EnvoyProxyClusterFixture()
    {
        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Creating Redis container...");

        _container = new RedisBuilder()
            .WithImage(RedisImage)
            .WithName(RedisContainerName)
            .WithPortBinding(RedisContainerPort, RedisBuilder.RedisPort)
            .Build();

        _container.StartAsync().GetAwaiter().GetResult();
        
        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Redis container created");
        
        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Creating Envoy container...");
        
        var envoyYamlSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "envoy", "envoy.yaml");
        
        _envoyContainer = new ContainerBuilder()
            .WithImage(EnovyImage)
            .WithName(EnovyContainerName)
            .WithPortBinding(EnvoyContainerPort, EnvoyContainerPort)
            .DependsOn(_container)
            .WithBindMount(envoyYamlSource, "/etc/envoy/envoy.yaml", AccessMode.ReadOnly)
            .Build();

        _envoyContainer.StartAsync().GetAwaiter().GetResult();
        
        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Envoy container created");

        Task.Delay(TimeSpan.FromSeconds(30)).Wait();

        _multiplexer = ConnectionMultiplexer.Connect($"{_envoyContainer.Hostname}:{EnvoyContainerPort}", x =>
        {
            x.Proxy = Proxy.Envoyproxy;
            x.AbortOnConnectFail = false;
            x.ConnectRetry = 10;
            x.ConnectTimeout = 10000;
        });
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
        _envoyContainer.StopAsync().GetAwaiter().GetResult();
        _envoyContainer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _container.StopAsync().GetAwaiter().GetResult();
        _container.DisposeAsync().AsTask().GetAwaiter().GetResult();

        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Test containers pruned");
    }

    public async ValueTask DisposeAsync()
    {
        await _multiplexer.DisposeAsync();
        await _envoyContainer.StopAsync();
        await _envoyContainer.DisposeAsync();
        await _container.StopAsync();
        await _container.DisposeAsync();

        Console.WriteLine(
            $"[redis-explorer-tests {TimeProvider.System.GetUtcNow().DateTime.ToString(CultureInfo.InvariantCulture)}] Test containers pruned");
    }
}

[UsedImplicitly]
[CollectionDefinition("SingleInstanceIntegrationTests")]
public class RedisExplorerTests : ICollectionFixture<SingleInstanceFixture>
{
    [Collection("SingleInstanceIntegrationTests")]
    public class Set
    {
        private readonly SingleInstanceFixture _singleInstanceFixture;

        public Set(SingleInstanceFixture singleInstanceFixture)
        {
            _singleInstanceFixture = singleInstanceFixture;
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
            _singleInstanceFixture.Initialize();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var now = TimeProvider.System.GetUtcNow();

            var key = _singleInstanceFixture.GetKey();
            
            // Act
            testObj.SetString(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
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
            
            expiration.Should().BeCloseTo(expAsDt, _singleInstanceFixture.AcceptedDelta);
        }
        
        [Fact]
        public void ShouldNotSetExpirationWhenOptionsEmpty()
        {
            // Arrange
            _singleInstanceFixture.Initialize();
            var testObj = _singleInstanceFixture.GetTestInstance();
            TimeProvider.System.GetUtcNow();

            var key = _singleInstanceFixture.GetKey();
            
            // Act
            var result = testObj.Set(key, _singleInstanceFixture.TestString);
            
            // Assert
            var expiration = testObj.GetDatabase().KeyExpireTime(key);

            expiration.Should().BeNull();
            result.IsSuccess.Should().BeTrue();
        }
        
[Fact]
        public void ShouldNotOverwriteTheKeyIfOptionsSetToNoOverwriting()
        {
            // Arrange
            _singleInstanceFixture.Initialize();
            var testObj = _singleInstanceFixture.GetTestInstance();

            var key = _singleInstanceFixture.GetKey();
            var overwritten = _singleInstanceFixture.TestString + _singleInstanceFixture.TestString;
            
            // Act
            var result1 = testObj.SetString(key, _singleInstanceFixture.TestString);
            var result2 = testObj.SetString(key, overwritten, opt => opt.WithoutKeyOverwriting());
            var getResult = testObj.GetString(key);
            
            // Assert
            result1.IsSuccess.Should().BeTrue();
            result1.KeyOverwritten.Should().BeFalse();
            result1.KeyCollisionOccurred.Should().BeNull();
            result2.IsSuccess.Should().BeFalse();
            result2.KeyOverwritten.Should().BeFalse();
            result2.KeyCollisionOccurred.Should().BeTrue();
            getResult.IsDefined(out var sub).Should().BeTrue();
            sub.Should().Be(_singleInstanceFixture.TestString);
        }
        
        [Fact]
        public void ShouldOverwriteTheKeyIfOptionsSetToOverwriting()
        {
            // Arrange
            _singleInstanceFixture.Initialize();
            var testObj = _singleInstanceFixture.GetTestInstance();

            var key = _singleInstanceFixture.GetKey();

            var overwritten = _singleInstanceFixture.TestString + _singleInstanceFixture.TestString;
            
            // Act
            var result1 = testObj.SetString(key, _singleInstanceFixture.TestString);
            var result2 = testObj.SetString(key, overwritten);
            var getResult = testObj.GetString(key);
            
            // Assert
            result1.IsSuccess.Should().BeTrue();
            result1.KeyOverwritten.Should().BeFalse();
            result2.IsSuccess.Should().BeTrue();
            result2.KeyOverwritten.Should().BeTrue();

            getResult.IsDefined(out var sub).Should().BeTrue();
            sub.Should().Be(overwritten);
        }
    }
    
    [Collection("SingleInstanceIntegrationTests")]
    public class SetAsync
    {
        private readonly SingleInstanceFixture _singleInstanceFixture;

        public SetAsync(SingleInstanceFixture singleInstanceFixture)
        {
            _singleInstanceFixture = singleInstanceFixture;
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
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var now = TimeProvider.System.GetUtcNow();

            var key = _singleInstanceFixture.GetKey();
            
            // Act
            await testObj.SetStringAsync(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            // Assert
            var expiration = await (testObj.GetDatabase()).KeyExpireTimeAsync(key);
            
            expiration.Should().NotBeNull();
            expiration!.Value.Should().BeAfter(now.DateTime);
            
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            
            expiration.Should().BeCloseTo(expAsDt, _singleInstanceFixture.AcceptedDelta);
        }
        
        [Fact]
        public async Task ShouldNotSetExpirationWhenOptionsEmpty()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();

            var key = _singleInstanceFixture.GetKey();
            
            // Act
            var result = await testObj.SetAsync(key, _singleInstanceFixture.TestString);
            
            // Assert
            var expiration = await (testObj.GetDatabase()).KeyExpireTimeAsync(key);

            expiration.Should().BeNull();
            result.IsSuccess.Should().BeTrue();
        }
        
        [Fact]
        public async Task ShouldNotOverwriteTheKeyIfOptionsSetToNoOverwriting()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();

            var key = _singleInstanceFixture.GetKey();
            var overwritten = _singleInstanceFixture.TestString + _singleInstanceFixture.TestString;
            
            // Act
            var result1 = await testObj.SetStringAsync(key, _singleInstanceFixture.TestString);
            var result2 = await testObj.SetStringAsync(key, overwritten, opt => opt.WithoutKeyOverwriting());
            var getResult = await testObj.GetStringAsync(key);
            
            // Assert
            result1.IsSuccess.Should().BeTrue();
            result1.KeyOverwritten.Should().BeFalse();
            result1.KeyCollisionOccurred.Should().BeNull();
            result2.IsSuccess.Should().BeFalse();
            result2.KeyOverwritten.Should().BeFalse();
            result2.KeyCollisionOccurred.Should().BeTrue();
            getResult.IsDefined(out var sub).Should().BeTrue();
            sub.Should().Be(_singleInstanceFixture.TestString);
        }
        
        [Fact]
        public async Task ShouldOverwriteTheKeyIfOptionsSetToOverwriting()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();

            var key = _singleInstanceFixture.GetKey();

            var overwritten = _singleInstanceFixture.TestString + _singleInstanceFixture.TestString;
            
            // Act
            var result1 = await testObj.SetStringAsync(key, _singleInstanceFixture.TestString);
            var result2 = await testObj.SetStringAsync(key, overwritten);
            var getResult = await testObj.GetStringAsync(key);
            
            // Assert
            result1.IsSuccess.Should().BeTrue();
            result1.KeyOverwritten.Should().BeFalse();
            result2.IsSuccess.Should().BeTrue();
            result2.KeyOverwritten.Should().BeTrue();

            getResult.IsDefined(out var sub).Should().BeTrue();
            sub.Should().Be(overwritten);
        }
    }
    
    [Collection("SingleInstanceIntegrationTests")]
    public class Get
    {
        private readonly SingleInstanceFixture _singleInstanceFixture;

        public Get(SingleInstanceFixture singleInstanceFixture)
        {
            _singleInstanceFixture = singleInstanceFixture;
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
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            testObj.SetString(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            var preGetExpiration = testObj.GetDatabase().KeyExpireTime(key);

            await Task.Delay(_singleInstanceFixture.Delay);
            
            var result = testObj.Get(key);
            
            var postGetExpiration = testObj.GetDatabase().KeyExpireTime(key);
            
            // Assert
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            preGetExpiration.Should().Be(postGetExpiration);
        }
        
        public static object[][] SlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(3) } },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(6), AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(10)), SlidingExpiration = TimeSpan.FromMinutes(1)} },
                new object[] { new DistributedCacheEntryOptions(){ AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(7), SlidingExpiration = TimeSpan.FromMinutes(3) } },
                new object[] { new DistributedCacheEntryOptions(){ SlidingExpiration = TimeSpan.FromMinutes(4), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) } }
            };
        
        [Theory]
        [MemberData(nameof(SlidingExpirationTestCases))]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldRefreshExpirationWhenSlidingExpirationNonNull(DistributedCacheEntryOptions options)
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            var now = TimeProvider.System.GetUtcNow();
            
            // Act
            _ = testObj.SetString(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            var preGetExpiration = testObj.GetDatabase().KeyExpireTime(key);

            await Task.Delay(_singleInstanceFixture.Delay);
            
            var result = testObj.Get(key);
            
            var postGetExpiration = testObj.GetDatabase().KeyExpireTime(key);
            
            // Assert
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            var preExpAsDt = expAsDt;
            var postExpAsDt = expAsDt.Add(_singleInstanceFixture.Delay);
            
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            preGetExpiration.Should().NotBe(postGetExpiration);
            result.IsSuccess.Should().BeTrue();
            postGetExpiration!.Value.Should().BeAfter(preGetExpiration!.Value);
            postGetExpiration.Should().BeCloseTo(postExpAsDt, _singleInstanceFixture.AcceptedDelta);
            preGetExpiration.Should().BeCloseTo(preExpAsDt, _singleInstanceFixture.AcceptedDelta);
        }

        [Fact]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldReturnDefinedResultWithCorrectValueWhenKeyExists()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            testObj.SetString(key, _singleInstanceFixture.TestString);
            
            var result = testObj.GetString(key);
            
            // Assert
            result.IsDefined(out var sub);
            sub.Should().Be(_singleInstanceFixture.TestString);
        }
        
        [Fact]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldReturnResultWithNotFoundFlagWhenKeyDoesntExist()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            var result = testObj.GetString(key);
            
            // Assert
            result.IsDefined(out var sub).Should().BeFalse();
            sub.Should().BeNull();
            result.IsSuccess.Should().BeFalse();
            result.KeyNotFound.Should().BeTrue();
        }
    }
    
    [Collection("SingleInstanceIntegrationTests")]
    public class GetAsync
    {
        private readonly SingleInstanceFixture _singleInstanceFixture;

        public GetAsync(SingleInstanceFixture singleInstanceFixture)
        {
            _singleInstanceFixture = singleInstanceFixture;
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
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            await testObj.SetStringAsync(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            var preGetExpiration = await (testObj.GetDatabase()).KeyExpireTimeAsync(key);

            await Task.Delay(_singleInstanceFixture.Delay);
            
            var result = await testObj.GetAsync(key);
            
            var postGetExpiration = await (testObj.GetDatabase()).KeyExpireTimeAsync(key);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
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
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            var now = TimeProvider.System.GetUtcNow();
            
            // Act
            await testObj.SetStringAsync(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            var preGetExpiration =  await (testObj.GetDatabase()).KeyExpireTimeAsync(key);

            await Task.Delay(_singleInstanceFixture.Delay);
            
            var result = await testObj.GetAsync(key);
            
            var postGetExpiration = await (testObj.GetDatabase()).KeyExpireTimeAsync(key);
            
            // Assert
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            var preExpAsDt = expAsDt;
            var postExpAsDt = expAsDt.Add(_singleInstanceFixture.Delay);
            
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            preGetExpiration.Should().NotBe(postGetExpiration);
            postGetExpiration!.Value.Should().BeAfter(preGetExpiration!.Value);
            postGetExpiration.Should().BeCloseTo(postExpAsDt, _singleInstanceFixture.AcceptedDelta);
            preGetExpiration.Should().BeCloseTo(preExpAsDt, _singleInstanceFixture.AcceptedDelta);
        }
        
        [Fact]
        public async Task ShouldReturnDefinedResultWithCorrectValueWhenKeyExists()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            await testObj.SetStringAsync(key, _singleInstanceFixture.TestString);
            
            var result = await testObj.GetStringAsync(key);
            
            // Assert
            result.IsDefined(out var sub);
            sub.Should().Be(_singleInstanceFixture.TestString);
        }
        
        [Fact]
        public async Task ShouldReturnResultWithNotFoundFlagWhenKeyDoesntExist()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            var result = await testObj.GetStringAsync(key);
            
            // Assert
            result.IsDefined(out var sub).Should().BeFalse();
            sub.Should().BeNull();
            result.IsSuccess.Should().BeFalse();
            result.KeyNotFound.Should().BeTrue();
        }
    }
    
    [Collection("SingleInstanceIntegrationTests")]
    public class Refresh
    {
        private readonly SingleInstanceFixture _singleInstanceFixture;

        public Refresh(SingleInstanceFixture singleInstanceFixture)
        {
            _singleInstanceFixture = singleInstanceFixture;
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
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            testObj.SetString(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            var preGetExpiration = testObj.GetDatabase().KeyExpireTime(key);

            await Task.Delay(_singleInstanceFixture.Delay);
            
            var result = testObj.Refresh(key);
            
            var postGetExpiration = testObj.GetDatabase().KeyExpireTime(key);
            
            // Assert
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            result.KeyHasNoSlidingExpiration.Should().BeTrue();
            result.IsSuccess.Should().BeFalse();
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
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            var now = TimeProvider.System.GetUtcNow();
            
            // Act
            testObj.SetString(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            var preGetExpiration = testObj.GetDatabase().KeyExpireTime(key);

            await Task.Delay(_singleInstanceFixture.Delay);
            
            var result = testObj.Refresh(key);
            
            var postGetExpiration = testObj.GetDatabase().KeyExpireTime(key);
            
            // Assert
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            var preExpAsDt = expAsDt;
            var postExpAsDt = expAsDt.Add(_singleInstanceFixture.Delay);
            
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.KeyHasNoSlidingExpiration.Should().BeFalse();
            preGetExpiration.Should().NotBe(postGetExpiration);
            postGetExpiration!.Value.Should().BeAfter(preGetExpiration!.Value);
            postGetExpiration.Should().BeCloseTo(postExpAsDt, _singleInstanceFixture.AcceptedDelta);
            preGetExpiration.Should().BeCloseTo(preExpAsDt, _singleInstanceFixture.AcceptedDelta);
        }
    }
    
    [Collection("SingleInstanceIntegrationTests")]
    public class RefreshAsync
    {
        private readonly SingleInstanceFixture _singleInstanceFixture;

        public RefreshAsync(SingleInstanceFixture singleInstanceFixture)
        {
            _singleInstanceFixture = singleInstanceFixture;
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
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            await testObj.SetStringAsync(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            var preGetExpiration = await (testObj.GetDatabase()).KeyExpireTimeAsync(key);

            await Task.Delay(_singleInstanceFixture.Delay);
            
            var result = await testObj.RefreshAsync(key);
            
            var postGetExpiration = await (testObj.GetDatabase()).KeyExpireTimeAsync(key);
            
            // Assert
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.KeyHasNoSlidingExpiration.Should().BeTrue();
            preGetExpiration.Should().Be(postGetExpiration);
        }
        
        public static object[][] SlidingExpirationTestCases()
            => new[]
            {
                new object[] { new DistributedCacheEntryOptions(){  SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){   AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), 
                    AbsoluteExpiration = TimeProvider.System.GetUtcNow().Add(TimeSpan.FromMinutes(10)), SlidingExpiration = TimeSpan.FromMinutes(1)} },
                new object[] { new DistributedCacheEntryOptions(){   AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), SlidingExpiration = TimeSpan.FromMinutes(1) } },
                new object[] { new DistributedCacheEntryOptions(){  SlidingExpiration = TimeSpan.FromMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) } }
            };
        
        [Theory]
        [MemberData(nameof(SlidingExpirationTestCases))]
        public async Task ShouldRefreshExpirationWhenSlidingExpirationNonNull(DistributedCacheEntryOptions options)
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            var now = TimeProvider.System.GetUtcNow();
            
            // Act
            await testObj.SetStringAsync(key, _singleInstanceFixture.TestString, x => x.WithExpirationOptions(options));
            
            var preGetExpiration =  await (testObj.GetDatabase()).KeyExpireTimeAsync(key);

            await Task.Delay(_singleInstanceFixture.Delay);
            
            var result = await testObj.RefreshAsync(key);
            
            var postGetExpiration = await (testObj.GetDatabase()).KeyExpireTimeAsync(key);
            
            // Assert
            var exp = options.SlidingExpiration.HasValue 
                ? now.Add(options.SlidingExpiration.Value)
                : options.AbsoluteExpirationRelativeToNow.HasValue 
                    ? now.Add(options.AbsoluteExpirationRelativeToNow.Value)
                    : options.AbsoluteExpiration;
            
            var expAsDt = exp!.Value.DateTime;
            var preExpAsDt = expAsDt;
            var postExpAsDt = expAsDt.Add(_singleInstanceFixture.Delay);
            
            preGetExpiration.Should().NotBeNull();
            postGetExpiration.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.KeyHasNoSlidingExpiration.Should().BeFalse();
            preGetExpiration.Should().NotBe(postGetExpiration);
            postGetExpiration!.Value.Should().BeAfter(preGetExpiration!.Value);
            postGetExpiration.Should().BeCloseTo(postExpAsDt, _singleInstanceFixture.AcceptedDelta);
            preGetExpiration.Should().BeCloseTo(preExpAsDt, _singleInstanceFixture.AcceptedDelta);
        }
    }
    
    [Collection("SingleInstanceIntegrationTests")]
    public class Remove
    {
        private readonly SingleInstanceFixture _singleInstanceFixture;

        public Remove(SingleInstanceFixture singleInstanceFixture)
        {
            _singleInstanceFixture = singleInstanceFixture;
        }

        [Fact]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldReturnUnsuccessfulResultWhenKeyDoesntExist()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            var result = testObj.Remove(key);
            var getResult = testObj.GetString(key);
            
            // Assert
            result.KeyNotFound.Should().BeTrue();
            result.IsSuccess.Should().BeFalse();
            getResult.IsSuccess.Should().BeFalse();
        }
        
        
        [Fact]
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
        public async Task ShouldReturnSuccessfulResultWhenKeyExists()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            var setResult = testObj.SetString(key, _singleInstanceFixture.TestString);
            var result = testObj.Remove(key);
            var getResult = testObj.GetString(key);
            
            // Assert
            setResult.IsSuccess.Should().BeTrue();
            result.KeyNotFound.Should().BeFalse();
            result.IsSuccess.Should().BeTrue();
            getResult.IsSuccess.Should().BeFalse();
        }
    }
    
    [Collection("SingleInstanceIntegrationTests")]
    public class RemoveAsync
    {
        private readonly SingleInstanceFixture _singleInstanceFixture;

        public RemoveAsync(SingleInstanceFixture singleInstanceFixture)
        {
            _singleInstanceFixture = singleInstanceFixture;
        }

        [Fact]
        public async Task ShouldReturnUnsuccessfulResultWhenKeyDoesntExist()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            var result = await testObj.RemoveAsync(key);
            var getResult = await testObj.GetStringAsync(key);
            
            // Assert
            result.KeyNotFound.Should().BeTrue();
            result.IsSuccess.Should().BeFalse();
            getResult.IsSuccess.Should().BeFalse();
        }
        
        
        [Fact]
        public async Task ShouldReturnSuccessfulResultWhenKeyExists()
        {
            // Arrange
            await _singleInstanceFixture.InitializeAsync();
            var testObj = _singleInstanceFixture.GetTestInstance();
            var key = _singleInstanceFixture.GetKey();
            
            // Act
            var setResult = await testObj.SetStringAsync(key, _singleInstanceFixture.TestString);
            var result = await testObj.RemoveAsync(key);
            var getResult = await testObj.GetStringAsync(key);
            
            // Assert
            setResult.IsSuccess.Should().BeTrue();
            result.KeyNotFound.Should().BeFalse();
            result.IsSuccess.Should().BeTrue();
            getResult.IsSuccess.Should().BeFalse();
        }
    }

    private sealed record TestObj(string Test);
}
