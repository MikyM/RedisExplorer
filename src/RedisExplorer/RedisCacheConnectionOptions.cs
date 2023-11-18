using Microsoft.Extensions.Logging;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis.Configuration;

namespace RedisExplorer;

/// <summary>
/// Represents the base options for connecting to Redis.
/// </summary>
[PublicAPI]
public class RedisCacheConnectionOptions : IOptions<RedisCacheConnectionOptions>
{
    /// <summary>
    /// Creates a new instance of <see cref="RedisCacheConnectionOptions"/>.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="isConnectionMultiplexerOwned">Whether the multiplexer should be owned by <see cref="IRedisExplorer"/>.</param>
    public RedisCacheConnectionOptions(string configuration, bool isConnectionMultiplexerOwned = true)
    {
        Configuration = configuration;
        IsConnectionMultiplexerOwned = isConnectionMultiplexerOwned;
    }
    
    /// <summary>
    /// Creates a new instance of <see cref="RedisCacheConnectionOptions"/>.
    /// </summary>
    /// <param name="configurationOptions">The configuration options.</param>
    /// <param name="isConnectionMultiplexerOwned">Whether the multiplexer should be owned by <see cref="IRedisExplorer"/>.</param>
    public RedisCacheConnectionOptions(ConfigurationOptions configurationOptions, bool isConnectionMultiplexerOwned = true)
    {
        ConfigurationOptions = configurationOptions;
        IsConnectionMultiplexerOwned = isConnectionMultiplexerOwned;
    }
    
    /// <summary>
    /// Creates a new instance of <see cref="RedisCacheConnectionOptions"/>.
    /// </summary>
    /// <param name="connectionMultiplexerFactory">The factory method.</param>
    /// <param name="isConnectionMultiplexerOwned">Whether the multiplexer should be owned by <see cref="IRedisExplorer"/>.</param>
    public RedisCacheConnectionOptions(Func<Task<IConnectionMultiplexer>> connectionMultiplexerFactory, bool isConnectionMultiplexerOwned = true)
    {
        ConnectionMultiplexerFactory = connectionMultiplexerFactory;
        IsConnectionMultiplexerOwned = isConnectionMultiplexerOwned;
    }
    
    /// <summary>
    /// Gets or sets a delegate to create the DistributedLockFactory instance.
    /// </summary>
    public Func<IConnectionMultiplexer,ILoggerFactory,Task<IDistributedLockFactory>> DistributedLockFactory { get; init; }
        = (multiplexer,loggerFactory) => Task.FromResult<IDistributedLockFactory>(RedLockFactory.Create(new List<RedLockMultiplexer>()
            { new(multiplexer) { RedisKeyFormat = "redis-explorer-lock:{0}" } }, loggerFactory));
    
    /// <summary>
    /// Gets or sets whether the instance returned by <see cref="DistributedLockFactory"/>  should be owned by (and disposed with) <see cref="IRedisExplorer"/>. This defaults to true.
    /// </summary>
    public bool IsDistributedLockFactoryOwned { get; init; } = true;
    
    /// <summary>
    /// The configuration used to connect to Redis.
    /// </summary>
    public string? Configuration { get; }

    /// <summary>
    /// The configuration used to connect to Redis.
    /// This is preferred over Configuration.
    /// </summary>
    public ConfigurationOptions? ConfigurationOptions { get; }

    /// <summary>
    /// Gets or sets a delegate to create the ConnectionMultiplexer instance.
    /// </summary>
    public Func<Task<IConnectionMultiplexer>>? ConnectionMultiplexerFactory { get; }

    /// <summary>
    /// Gets or sets whether the instance returned by <see cref="ConnectionMultiplexerFactory"/> should be owned by (and disposed with) <see cref="IRedisExplorer"/>. This defaults to true.
    /// </summary>
    public bool IsConnectionMultiplexerOwned { get; }
    
    /// <summary>
    /// Gets the configured options.
    /// </summary>
    /// <param name="libSuffix">Lib suffix.</param>
    /// <returns>The configured options.</returns>
    internal ConfigurationOptions GetConfiguredOptions(string libSuffix)
    {
        var options = ConfigurationOptions?.Clone() ?? ConfigurationOptions.Parse(Configuration!);

        // we don't want an initially unavailable server to prevent DI creating the service itself
        options.AbortOnConnectFail = false;

        if (!string.IsNullOrWhiteSpace(libSuffix))
        {
            var provider = DefaultOptionsProvider.GetProvider(options.EndPoints);
            options.LibraryName = $"{provider.LibraryName} {libSuffix}";
        }
        
        return options;
    }

    RedisCacheConnectionOptions IOptions<RedisCacheConnectionOptions>.Value => this;

    /// <summary>
    /// Runs post configuration.
    /// </summary>
    public void PostConfigure()
    {
    }
}
