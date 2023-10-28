using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedisExplorer;

/// <summary>
/// Distributed cache implementation using Redis.
/// <para>Uses <c>StackExchange.Redis</c> as the Redis client.</para>
/// </summary>
[PublicAPI]
internal sealed class RedisExplorerImpl : RedisExplorer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RedisExplorerImpl"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="redisExplorerOptions">Cache settings.</param>
    public RedisExplorerImpl(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor, IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor,
        ILogger<RedisExplorer> logger, ILoggerFactory loggerFactory, ImmutableRedisExplorerOptions redisExplorerOptions)
        : base(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor, logger, loggerFactory, redisExplorerOptions)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisExplorerImpl"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="redisExplorerOptions">Cache settings.</param>
    public RedisExplorerImpl(TimeProvider timeProvider,IOptions<RedisCacheOptions> optionsAccessor, IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor, ImmutableRedisExplorerOptions redisExplorerOptions)
        : base(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor, redisExplorerOptions)
    {
    }
}
