﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RedisExplorer.OperationResults;
using RedLockNet;

namespace RedisExplorer;

/// <summary>
/// Distributed cache implementation using Redis.
/// <para>Uses <c>StackExchange.Redis</c> as the Redis client.</para>
/// </summary>
[PublicAPI]
public sealed class RedisExplorer : IRedisExplorer, IDistributedCache, IDisposable, IAsyncDisposable
{
    // combined keys - same hash keys fetched constantly; avoid allocating an array each time
    private static readonly RedisValue[] GetHashMembersWithRefresh = { LuaScripts.GetWithRefreshArg };
    private static readonly RedisValue[] HashMembersSingleNotPresent = { LuaScripts.NotPresent };
    private static readonly RedisValue[] HashMembersEmpty = Array.Empty<RedisValue>();
    private static RedisValue[] GetHashMembers(bool shouldRefresh) => shouldRefresh ? GetHashMembersWithRefresh : HashMembersSingleNotPresent;
    
    private Dictionary<string,byte[]> _knownInternalScripts = [];
    
    /// <summary>
    /// Json options name.
    /// </summary>
    public const string JsonOptionsName = "RedisExplorerJsonOptions";

    private const string ErrorMessage = "Failed to execute Redis explorer script, check exceptions for more details";
    
    private volatile IDistributedLockFactory? _lockFactory;
    private volatile IConnectionMultiplexer? _multiplexer;
    private volatile IDatabase? _redisDatabase;
    
    private bool _disposed;
    
    /// <inheritdoc/>
    public bool? UsingProxy => ConfigurationOptions?.Proxy != null && ConfigurationOptions.Proxy != Proxy.None;
    
    /// <inheritdoc/>
    public ConfigurationOptions? ConfigurationOptions { get; private set; }
    
    /// <inheritdoc/>
    public RedisKey Prefix { get; }
    
    /// <inheritdoc/>
    public RedisCacheOptions Options { get; }
    
    /// <inheritdoc/>
    public JsonSerializerOptions JsonSerializerOptions { get; }

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    
    private readonly ILoggerFactory _loggerFactory;
    
    private readonly SemaphoreSlim _connectionLock = new(1,1);

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor)
        : this(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor, NullLoggerFactory.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="configurationOptions">The Redis configuration options.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor, ConfigurationOptions configurationOptions)
        : this(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor, NullLoggerFactory.Instance)
    {
        ConfigurationOptions = configurationOptions;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="loggerFactory">The _logger factory.</param>
    /// <param name="configurationOptions">The Redis configuration options.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor,
        ILoggerFactory loggerFactory, ConfigurationOptions configurationOptions)
        : this(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor, loggerFactory)
    {
        ConfigurationOptions = configurationOptions;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="loggerFactory">The _logger factory.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        ArgumentNullException.ThrowIfNull(jsonSerializerOptionsAccessor);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        Options = optionsAccessor.Value;
        JsonSerializerOptions = jsonSerializerOptionsAccessor.Get(JsonOptionsName);
        _logger = loggerFactory.CreateLogger<RedisExplorer>();
        _loggerFactory = loggerFactory;

        // This allows partitioning a single backend cache for use with multiple apps/services.
        var prefix = Options.Prefix;
        if (!string.IsNullOrEmpty(prefix))
        {
            // SE.Redis allows efficient append of key-prefix scenarios, but we can help it
            // avoid some work/allocations by forcing the key-prefix to be a byte[]; SE.Redis
            // would do this itself anyway, using UTF8
            Prefix = (RedisKey)Encoding.UTF8.GetBytes(prefix);
        }
        
        PreHashScripts();
    }

    private bool ShouldUseSHAOptimization()
        => UsingProxy.HasValue && UsingProxy.Value && Options.UseBandwidthOptimizationForProxies;
    
    private const string NoScript = "NOSCRIPT";

    private void PreHashScripts()
    {
        _knownInternalScripts.TryAdd(nameof(LuaScripts.GetAndRefreshScript), CalculateScriptHash(LuaScripts.GetAndRefreshScript));
        _knownInternalScripts.TryAdd(nameof(LuaScripts.RefreshScript), CalculateScriptHash(LuaScripts.RefreshScript));
        _knownInternalScripts.TryAdd(nameof(LuaScripts.SetScript), CalculateScriptHash(LuaScripts.SetScript));
        _knownInternalScripts.TryAdd(nameof(LuaScripts.RemoveScript), CalculateScriptHash(LuaScripts.RemoveScript));
    }

    private static PropertyInfo ConfigurationOptionsGetter =
        typeof(ConnectionMultiplexer).GetProperty("RawConfig", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find RawConfig property on ConnectionMultiplexer type.");

    private static ConfigurationOptions GetOptions(IConnectionMultiplexer multiplexer)
        => ((ConfigurationOptions)ConfigurationOptionsGetter.GetValue((ConnectionMultiplexer)multiplexer)!).Clone();
    
    /// <summary>
    /// Calculates the hash of a script.
    /// </summary>
    /// <param name="script">Script.</param>
    /// <returns>Hash.</returns>
    public static byte[] CalculateScriptHash(string script)
    {
        var scriptBytes = Encoding.ASCII.GetBytes(script);
        return SHA1.HashData(scriptBytes);
    }
    
    /// <inheritdoc/>
    public IDatabase GetDatabase()
    {
        CheckDisposed();

        var connect = Connect();

        return connect.RedisDatabase;
    }

    /// <inheritdoc/>
    public IConnectionMultiplexer GetMultiplexer()
    {
        CheckDisposed();

        var connect = Connect();

        return connect.Multiplexer;
    }

    /// <inheritdoc/>
    public IDistributedLockFactory GetLockFactory()
    {
        CheckDisposed();

        var connect = Connect();

        return connect.LockFactory;
    }

    /// <inheritdoc />
    public string GetPrefixedKey(string key)
    {
        CheckDisposed();
        return $"{Options.Prefix}{key}";
    }
    
    #region Lock factory implementation

    /// <inheritdoc cref="IRedisExplorer.CreateLock(string, TimeSpan)"/>
    public IRedLock CreateLock(string resource, TimeSpan expiryTime)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(resource);
        
        var (_, lockFactory, _) = Connect();

        return lockFactory.CreateLock(resource, expiryTime);
    }

    /// <inheritdoc cref="IRedisExplorer.CreateLockAsync(string, TimeSpan, CancellationToken)"/>
    public async Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime, CancellationToken cancellationToken = default)
    {
        CheckDisposed();
        
        cancellationToken.ThrowIfCancellationRequested();
        
        ArgumentNullException.ThrowIfNull(resource);

        var (_, lockFactory, _) = await ConnectAsync(cancellationToken);
        
        return (await lockFactory.CreateLockAsync(resource, expiryTime));
    }
    

    /// <inheritdoc cref="IRedisExplorer.CreateLock(string, TimeSpan, TimeSpan, TimeSpan, CancellationToken)"/>
    public IRedLock CreateLock(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime, CancellationToken cancellationToken = default)
    {
        CheckDisposed();
        
        cancellationToken.ThrowIfCancellationRequested();
        
        ArgumentNullException.ThrowIfNull(resource);
        
        var (_, lockFactory, _) = Connect();

        return lockFactory.CreateLock(resource, expiryTime, waitTime, retryTime, cancellationToken);
    }

    /// <inheritdoc cref="IRedisExplorer.CreateLockAsync(string, TimeSpan, TimeSpan, TimeSpan, CancellationToken)"/>
    public async Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime,
        CancellationToken cancellationToken = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(resource);

        var (_, lockFactory, _) = await ConnectAsync(cancellationToken);
        
        return (await lockFactory.CreateLockAsync(resource, expiryTime, waitTime, retryTime, cancellationToken));
    }

    #endregion

    #region Connection

    private void PrepareConnection(IConnectionMultiplexer connection)
    {
        ValidateServerFeatures(connection);
        TryRegisterProfiler(connection);
    }

    private void ValidateServerFeatures(IConnectionMultiplexer connection)
    {
        CheckDisposed();
        
        _ = connection ?? throw new InvalidOperationException($"{nameof(connection)} cannot be null.");
    }

    private void TryRegisterProfiler(IConnectionMultiplexer connection)
    {
        CheckDisposed();
        
        _ = connection ?? throw new InvalidOperationException($"{nameof(connection)} cannot be null.");

        if (Options.ProfilingSession is not null)
        {
            connection.RegisterProfiler(Options.ProfilingSession);
        }
    }
    
    [MemberNotNull(nameof(_lockFactory), nameof(_multiplexer), nameof(_redisDatabase))]
    private (IConnectionMultiplexer Multiplexer, IDistributedLockFactory LockFactory, IDatabase RedisDatabase) Connect()
    {
        CheckDisposed();

        var multiplexer = _multiplexer;
        var lockFactory = _lockFactory;
        var redisDatabase = _redisDatabase;
        
        if (lockFactory is not null && multiplexer is not null && redisDatabase is not null)
        {
            Debug.Assert(_lockFactory is not null);
            Debug.Assert(_multiplexer is not null);
            Debug.Assert(_redisDatabase is not null);
            Debug.Assert(ConfigurationOptions is not null);
            
            return new (multiplexer, lockFactory, redisDatabase);
        }

        _connectionLock.Wait();
        
        try
        {
            // check again in case some process finished connecting prior to us waiting on the lock
            multiplexer = _multiplexer;
            lockFactory = _lockFactory;
            redisDatabase = _redisDatabase;
            
            if (lockFactory is not null && multiplexer is not null && redisDatabase is not null)
            {
                Debug.Assert(_lockFactory is not null);
                Debug.Assert(_multiplexer is not null);
                Debug.Assert(_redisDatabase is not null);
                Debug.Assert(ConfigurationOptions is not null);
                
                return new (multiplexer, lockFactory, redisDatabase);
            }
            
            IConnectionMultiplexer connection;
                
            if (Options.ConnectionOptions.ConnectionMultiplexerFactory is null)
            {
                connection = ConnectionMultiplexer.Connect(Options.ConnectionOptions.GetConfiguredOptions("RedisExplorer"));
            }
            else
            {
                connection = Options.ConnectionOptions.ConnectionMultiplexerFactory().GetAwaiter().GetResult();
            }

            PrepareConnection(connection);

            multiplexer = connection;
                
            _ = Interlocked.Exchange(ref _multiplexer, multiplexer);

            redisDatabase = multiplexer.GetDatabase();

            _ = Interlocked.Exchange(ref _redisDatabase, redisDatabase);
                
            lockFactory = Options.ConnectionOptions.DistributedLockFactory(multiplexer, _loggerFactory).GetAwaiter().GetResult();
                
            _ = Interlocked.Exchange(ref _lockFactory, lockFactory);
            
            if (ConfigurationOptions is null)
            {
                var options = GetOptions(multiplexer);
                ConfigurationOptions = options;
            }
            
            Debug.Assert(_lockFactory is not null);
            Debug.Assert(_multiplexer is not null);
            Debug.Assert(_redisDatabase is not null);
            Debug.Assert(ConfigurationOptions is not null);
            
            return new (multiplexer, lockFactory, redisDatabase);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    [MemberNotNull(nameof(_lockFactory), nameof(_multiplexer), nameof(_redisDatabase))]
    private ValueTask<(IConnectionMultiplexer Multiplexer, IDistributedLockFactory LockFactory, IDatabase RedisDatabase)> ConnectAsync(CancellationToken token = default)
    {
        CheckDisposed();
        
        token.ThrowIfCancellationRequested();
        
        var lockFactory = _lockFactory;
        var multiplexer = _multiplexer;
        var redisDatabase = _redisDatabase;
        
        if (lockFactory is not null && multiplexer is not null && redisDatabase is not null)
        {
            Debug.Assert(_lockFactory is not null);
            Debug.Assert(_multiplexer is not null);
            Debug.Assert(_redisDatabase is not null);
            Debug.Assert(ConfigurationOptions is not null);
            
            return ValueTask.FromResult<(IConnectionMultiplexer Multiplexer, IDistributedLockFactory LockFactory, IDatabase RedisDatabase)>(new (multiplexer, lockFactory, redisDatabase));
        }
        
        return ConnectSlowAsync(token);
    }

    [MemberNotNull(nameof(_lockFactory), nameof(_multiplexer), nameof(_redisDatabase))]
    private async ValueTask<(IConnectionMultiplexer Multiplexer, IDistributedLockFactory LockFactory, IDatabase RedisDatabase)> ConnectSlowAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        
        await _connectionLock.WaitAsync(token);
        
        try
        {
            // check again in case some process finished connecting prior to us waiting on the lock
            var multiplexer = _multiplexer;
            var lockFactory = _lockFactory;
            var redisDatabase = _redisDatabase;
            
            if (lockFactory is not null && multiplexer is not null && redisDatabase is not null)
            {
                Debug.Assert(_lockFactory is not null);
                Debug.Assert(_multiplexer is not null);
                Debug.Assert(_redisDatabase is not null);
                Debug.Assert(ConfigurationOptions is not null);
                
                return new (multiplexer, lockFactory, redisDatabase);
            }
            
            IConnectionMultiplexer connection;
                
            if (Options.ConnectionOptions.ConnectionMultiplexerFactory is null)
            {
                connection = await ConnectionMultiplexer.ConnectAsync(Options.ConnectionOptions.GetConfiguredOptions("RedisExplorer"));
            }
            else
            {
                connection = await Options.ConnectionOptions.ConnectionMultiplexerFactory();
            }

            PrepareConnection(connection);

            multiplexer = connection;
                
            _ = Interlocked.Exchange(ref _multiplexer, multiplexer);
                
            redisDatabase = multiplexer.GetDatabase();

            _ = Interlocked.Exchange(ref _redisDatabase, redisDatabase);
            
            lockFactory = await Options.ConnectionOptions.DistributedLockFactory(multiplexer, _loggerFactory);
                
            _ = Interlocked.Exchange(ref _lockFactory, lockFactory);
            
            if (ConfigurationOptions is null)
            {
                var options = GetOptions(multiplexer);
                ConfigurationOptions = options;
            }
            
            Debug.Assert(_lockFactory is not null);
            Debug.Assert(_multiplexer is not null);
            Debug.Assert(_redisDatabase is not null);
            Debug.Assert(ConfigurationOptions is not null);
            
            // we can ignore null warnings here because we just set the values and Database/LockFactory can't be null if Multiplexer is not null
            return new (multiplexer, lockFactory, redisDatabase);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    #endregion
    
    #region Result transformation
    
    private static string? RedisResultToString(RedisResult result) => (string?)result;
    private static byte[]? RedisResultToBytes(RedisResult result) => (byte[]?)result;
    private TValue? RedisResultToDeserialized<TValue>(RedisResult result) where TValue : class
    {
        if (result.IsNull)
            return null;

        var bytes = RedisResultToBytes(result);

        if (bytes is null)
            return null;

        return Deserialize<TValue>(bytes);
    }
    
    #endregion

    #region IDistributedCache implementation

    /// <inheritdoc />
    byte[]? IDistributedCache.Get(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        
        var result = GetPrivate(key, null);

        return HandleGetResult(key, result, RedisResultToBytes);
    }

    /// <inheritdoc />
    async Task<byte[]?> IDistributedCache.GetAsync(string key, CancellationToken token)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await GetAsyncPrivate(key, (GetOptions?)null, token);

        return HandleGetResult(key, result, RedisResultToBytes);
    }

    /// <inheritdoc />
    void IDistributedCache.Refresh(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        
        var result = RefreshPrivate(key);

        HandleRefreshResult(key, result);
    }

    /// <inheritdoc />
    async Task IDistributedCache.RefreshAsync(string key, CancellationToken token)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        token.ThrowIfCancellationRequested();

        var result = await RefreshAsyncPrivate(key, token);

        HandleRefreshResult(key, result);
    }

    /// <inheritdoc />
    void IDistributedCache.Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var result = SetPrivate(key, value, null, options);

        HandleSetResult(key, result);
    }

    /// <inheritdoc />
    async Task IDistributedCache.SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var result = await SetPrivateAsync(key, value, null, options, token);

        HandleSetResult(key, result);
    }
    
    /// <inheritdoc />
    void IDistributedCache.Remove(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = RemovePrivate(key);
        
        HandleRemoveResult(key, result);
    }

    /// <inheritdoc />
    async Task IDistributedCache.RemoveAsync(string key, CancellationToken token)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await RemovePrivateAsync(key, token);
        
        HandleRemoveResult(key, result);
    }
    
    #endregion

    #region Get

    #region GetString

    /// <inheritdoc />
    public IGetResult<string> GetString(string key, Action<GetOptions> options)
        => GetString(key, GetOptionsFromAction(options));

    /// <inheritdoc />
    public IGetResult<string> GetString(string key, GetOptions options)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);
        
        var result = GetPrivate(key, options);

        return HandleGetResultAsExplorerResult(key, result, RedisResultToString);
    }

    /// <inheritdoc />
    public IGetResult<string> GetString(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        
        var result = GetPrivate(key, null);

        return HandleGetResultAsExplorerResult(key, result, RedisResultToString);
    }

    /// <inheritdoc />
    public Task<IGetResult<string>> GetStringAsync(string key, Action<GetOptions> options, CancellationToken token = default)
        => GetStringAsync(key, GetOptionsFromAction(options), token);

    /// <inheritdoc />
    public async Task<IGetResult<string>> GetStringAsync(string key, GetOptions options, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);

        var result = await GetAsyncPrivate(key, options, token);

        return HandleGetResultAsExplorerResult(key, result, RedisResultToString);
    }

    /// <inheritdoc />
    public async Task<IGetResult<string>> GetStringAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await GetAsyncPrivate(key, (GetOptions?)null, token);

        return HandleGetResultAsExplorerResult(key, result, RedisResultToString);
    }

    #endregion

    #region GetBytes

    /// <inheritdoc />
    public IGetResult<byte[]> Get(string key, GetOptions options)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);

        var result = GetPrivate(key, options);

        return HandleGetResultAsExplorerResult(key, result, RedisResultToBytes);
    }

    /// <inheritdoc />
    public IGetResult<byte[]> Get(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = GetPrivate(key, null);

        return HandleGetResultAsExplorerResult(key, result, RedisResultToBytes);
    }

    /// <inheritdoc />
    public IGetResult<byte[]> Get(string key, Action<GetOptions> options)
        => Get(key, GetOptionsFromAction(options));

    /// <inheritdoc />
    public async Task<IGetResult<byte[]>> GetAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await GetAsyncPrivate(key, (GetOptions?)null, token);

        return HandleGetResultAsExplorerResult(key, result, RedisResultToBytes);
    }
    
    /// <inheritdoc />
    public async Task<IGetResult<byte[]>> GetAsync(string key, GetOptions options, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);

        var result = await GetAsyncPrivate(key, options, token);

        return HandleGetResultAsExplorerResult(key, result, RedisResultToBytes);
    }

    
    /// <inheritdoc />
    public Task<IGetResult<byte[]>> GetAsync(string key, Action<GetOptions> options, CancellationToken token = default)
        => GetAsync(key, GetOptionsFromAction(options), token);

    #endregion

    #region GetDeserialized

    /// <inheritdoc />
    public IGetResult<TValue> Get<TValue>(string key) where TValue : class
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = GetPrivate(key, null);
        
        return HandleGetResultAsExplorerDeserializedResult<TValue>(key, result);
    }
    
    /// <inheritdoc />
    public IGetResult<TValue> Get<TValue>(string key, GetOptions options) where TValue : class
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);

        var result = GetPrivate(key, options);
        
        return HandleGetResultAsExplorerDeserializedResult<TValue>(key, result);
    }

    /// <inheritdoc />
    public IGetResult<TValue> Get<TValue>(string key, Action<GetOptions> options)
        where TValue : class
        => Get<TValue>(key, GetOptionsFromAction(options));

    /// <inheritdoc />
    public async Task<IGetResult<TValue>> GetAsync<TValue>(string key, Action<GetOptions> options, CancellationToken token = default) where TValue : class
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);

        var result = await GetAsyncPrivate(key, options, token);
        
        return HandleGetResultAsExplorerDeserializedResult<TValue>(key, result);
    }

    /// <inheritdoc />
    public async Task<IGetResult<TValue>> GetAsync<TValue>(string key, GetOptions options, CancellationToken token = default) where TValue : class
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);

        var result = await GetAsyncPrivate(key, options, token);
        
        return HandleGetResultAsExplorerDeserializedResult<TValue>(key, result);
    }

    /// <inheritdoc />
    public async Task<IGetResult<TValue>> GetAsync<TValue>(string key, CancellationToken token = default) where TValue : class
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await GetAsyncPrivate(key, (GetOptions?)null, token);
        
        return HandleGetResultAsExplorerDeserializedResult<TValue>(key, result);
    }
    
    #endregion
    
    private static GetOptions GetOptionsFromAction(Action<GetOptions> action)
    {
        var opt = new GetOptions();
        action.Invoke(opt);
        return opt;
    }
    
    private RedisResult GetPrivate(string key, GetOptions? options)
    {
        var (_, _, redisDatabase) = Connect();

        var actualOptions = options ?? global::RedisExplorer.GetOptions.Default;
        
        var actualKey = Prefix.Append(key);
        
        return Process(redisDatabase, new[] { actualKey }, GetHashMembers(actualOptions.ShouldRefresh),
            _knownInternalScripts[nameof(LuaScripts.GetAndRefreshScript)], LuaScripts.GetAndRefreshScript);
    }
    
    private TValue? HandleGetResult<TValue>(string key,  RedisResult result, Func<RedisResult,TValue?> factory)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", (string?)result);
            return default;
        }

        if (result.IsNull)
        {
            return default;
        }

        return factory(result);
    }
    
    private GetResult<TValue> HandleGetResultAsExplorerResult<TValue>(string key,  RedisResult result, Func<RedisResult,TValue?> dataFactory) where TValue : class
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", (string?)result);
            return new GetResult<TValue>(key, result)
            {
                RedisErrorOccurred = true
            };
        }

        if (result.IsNull)
        {
            return new GetResult<TValue>(key, result)
            {
                KeyNotFound = true,
                ProcessRequirementErrorOccurred = true
            };
        }

        return new GetResult<TValue>(key, result, dataFactory.Invoke(result));
    }
    
    private GetResult<TValue> HandleGetResultAsExplorerDeserializedResult<TValue>(string key, RedisResult result) where TValue : class
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", (string?)result);
            return new GetResult<TValue>(key, result)
            {
                RedisErrorOccurred = true
            };
        }

        if (result.IsNull)
        {
            return new GetResult<TValue>(key, result)
            {
                KeyNotFound = true,
                ProcessRequirementErrorOccurred = true
            };
        }

        var deserialized = RedisResultToDeserialized<TValue>(result);
        if (deserialized is null)
        {
            return new GetResult<TValue>(key, result)
            {
                RedisErrorOccurred = true
            };
        }

        return new GetResult<TValue>(key, result, deserialized);
    }

    private Task<RedisResult> GetAsyncPrivate(string key, Action<GetOptions> options,
        CancellationToken token = default)
    {
        var opt = new GetOptions();
        options.Invoke(opt);
        
        return GetAsyncPrivate(key, opt, token);
    }
    
    private async Task<RedisResult> GetAsyncPrivate(string key, GetOptions? options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        var (_, _, redisDatabase) = await ConnectAsync(token);

        var actualOptions = options ?? global::RedisExplorer.GetOptions.Default;
        
        var actualKey = Prefix.Append(key);

        return await ProcessAsync(redisDatabase, new[] { actualKey }, GetHashMembers(actualOptions.ShouldRefresh),
            _knownInternalScripts[nameof(LuaScripts.GetAndRefreshScript)], LuaScripts.GetAndRefreshScript, token);
    }

    #endregion

    #region Refresh

    /// <inheritdoc />
    public IRefreshResult Refresh(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        
        var result = RefreshPrivate(key);

        return HandleRefreshResultAsExplorerResult(key, result);
    }

    /// <inheritdoc />
    public async Task<IRefreshResult> RefreshAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        token.ThrowIfCancellationRequested();

        var result = await RefreshAsyncPrivate(key, token);

        return HandleRefreshResultAsExplorerResult(key, result);
    }
    
    private RedisResult RefreshPrivate(string key)
    {
        var (_, _, redisDatabase) = Connect();

        var actualKey = Prefix.Append(key);
        
        return Process(redisDatabase, new [] { actualKey }, HashMembersEmpty, _knownInternalScripts[nameof(LuaScripts.RefreshScript)], 
            LuaScripts.RefreshScript);
    }
    
    private async Task<RedisResult> RefreshAsyncPrivate(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        var (_, _, redisDatabase) = await ConnectAsync(token);

        var actualKey = Prefix.Append(key);
        
        return await ProcessAsync(redisDatabase, new [] { actualKey }, HashMembersEmpty, _knownInternalScripts[nameof(LuaScripts.RefreshScript)], 
            LuaScripts.RefreshScript, token);
    }
    
    private void HandleRefreshResult(string key,  RedisResult result)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", (string?)result);
            return;
        }

        if (result.IsNull)
        {
            return;
        }
        
        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            _logger.LogCritical("Unexpected value type returned from Redis refresh script execution. Expected a string type. Actual: {ActualType}",
                result.Resp3Type.ToString());

            return;
        }
            
        if (resultString != LuaScripts.SuccessReturn)
        {
            _logger.LogCritical(
                "Unexpected value returned from Redis script refresh execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.SuccessReturn, resultString);
        }
    }
    
    private RefreshResult HandleRefreshResultAsExplorerResult(string key,  RedisResult result)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", (string?)result);
            return new RefreshResult(key, result)
            {
                RedisErrorOccurred = true
            };
        }

        if (result.IsNull)
        {
            return new RefreshResult(key, result)
            {
                KeyNotFound = true,
                ProcessRequirementErrorOccurred = true
            };
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            _logger.LogCritical("Unexpected value type returned from Redis refresh script execution. Expected a string type. Actual: {ActualType}",
                result.Resp3Type.ToString());

            return new RefreshResult(key, result)
            {
                RedisErrorOccurred = true
            };
        }
            
        if (resultString == LuaScripts.SuccessReturn)
        {
            return new RefreshResult(key, result);
        }
        
        if (resultString == LuaScripts.RefreshNoSlidingExpirationReturn)
        {
            return new RefreshResult(key, result)
            {
                KeyHasNoSlidingExpiration = true,
                ProcessRequirementErrorOccurred = true
            };
        }

        _logger.LogCritical(
            "Unexpected value returned from Redis refresh script execution. Expected: {ExpectedValue}. Actual: {ActualValue}",
            LuaScripts.SuccessReturn, resultString);

        return new RefreshResult(key, result)
        {
            RedisErrorOccurred = true
        };
    }

    #endregion

    #region Set

    #region SetString

    /// <inheritdoc />
    public Task<ISetResult> SetStringAsync(string key, string value, Action<SetOptions> options,
        CancellationToken token = default)
        => SetAsync(key, Encoding.UTF8.GetBytes(value), GetOptionsFromAction(options), token);

    /// <inheritdoc />
    public Task<ISetResult> SetStringAsync(string key, string value, SetOptions options, CancellationToken token = default)
        => SetAsync(key, Encoding.UTF8.GetBytes(value), options, token);

    /// <inheritdoc />
    public Task<ISetResult> SetStringAsync(string key, string value, CancellationToken token = default)
        => SetAsync(key, Encoding.UTF8.GetBytes(value), token);

    /// <inheritdoc />
    public ISetResult SetString(string key, string value, Action<SetOptions> options)
        => Set(key, value, GetOptionsFromAction(options));

    /// <inheritdoc />
    public ISetResult SetString(string key, string value, SetOptions options)
        => Set(key, Encoding.UTF8.GetBytes(value), options);

    /// <inheritdoc />
    public ISetResult SetString(string key, string value)
        => Set(key, Encoding.UTF8.GetBytes(value));

    #endregion
    
    #region SetBytes

    /// <inheritdoc />
    public Task<ISetResult> SetAsync(string key, byte[] value, Action<SetOptions> options, CancellationToken token = default)
        => SetAsync(key, value, GetOptionsFromAction(options), token);

    /// <inheritdoc />
    public async Task<ISetResult> SetAsync(string key, byte[] value, SetOptions options, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var result = await SetPrivateAsync(key, value, options, null, token);

        return HandleSetResultAsExplorerResult(key, result, !options.OverwriteIfKeyExists);
    }

    /// <inheritdoc />
    public async Task<ISetResult> SetAsync(string key, byte[] value, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var result = await SetPrivateAsync(key, value, null, null, token);

        return HandleSetResultAsExplorerResult(key, result, false);
    }
    
    /// <inheritdoc />
    public ISetResult Set(string key, byte[] value, SetOptions options)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var result = SetPrivate(key, value, options, null);

        return HandleSetResultAsExplorerResult(key, result, !options.OverwriteIfKeyExists);
    }
    /// <inheritdoc />
    public ISetResult Set(string key, byte[] value)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var result = SetPrivate(key, value, null, null);

        return HandleSetResultAsExplorerResult(key, result, false);
    }

    /// <inheritdoc />
    public ISetResult Set(string key, byte[] value, Action<SetOptions> options)
        => Set(key, value, GetOptionsFromAction(options));
    
    #endregion

    #region SetSerialized

    /// <inheritdoc />
    public Task<ISetResult> SetAsync<TValue>(string key, TValue value, Action<SetOptions> options, CancellationToken token = default) where TValue : class
        => SetAsync(key, SerializeToUtf8Bytes(value), options, token);

    /// <inheritdoc />
    public Task<ISetResult> SetAsync<TValue>(string key, TValue value, SetOptions options, CancellationToken token = default) where TValue : class
        => SetAsync(key, SerializeToUtf8Bytes(value), options, token);

    /// <inheritdoc />
    public Task<ISetResult> SetAsync<TValue>(string key, TValue value, CancellationToken token = default) where TValue : class
        => SetAsync(key, SerializeToUtf8Bytes(value), x => x.WithExpirationOptions(Options.ExpirationOptions.GetEntryOptions<TValue>()), token);
    
    /// <inheritdoc />
    public ISetResult Set<TValue>(string key, TValue value, Action<SetOptions> options) where TValue : class
        => Set(key, SerializeToUtf8Bytes(value), options);
    
    /// <inheritdoc />
    public ISetResult Set<TValue>(string key, TValue value, SetOptions options) where TValue : class
        => Set(key, SerializeToUtf8Bytes(value), options);
    
    /// <inheritdoc />
    public ISetResult Set<TValue>(string key, TValue value) where TValue : class
        => Set(key, SerializeToUtf8Bytes(value));

    #endregion

    private static SetOptions GetOptionsFromAction(Action<SetOptions> action)
    {
        var opt = new SetOptions();
        action.Invoke(opt);
        return opt;
    }
    
    private SetResult HandleSetResultAsExplorerResult(string key, RedisResult result, bool withCollisionDetection)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", result);
            return new SetResult(key, result)
            {
                RedisErrorOccurred = true
            };
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            _logger.LogCritical("Unexpected value type returned from Redis set script execution. Expected a string type. Actual: {Actual}",
                result.Resp3Type.ToString());

            return new SetResult(key, result)
            {
                RedisErrorOccurred = true
            };
        }
        
        if (resultString == LuaScripts.SuccessReturn)
        {
            return new SetResult(key, result)
            {
                KeyCollisionOccurred = withCollisionDetection ? false : null
            };
        }
        
        if (resultString == LuaScripts.SetOverwrittenReturn)
        {
            return new SetResult(key, result)
            {
                KeyOverwritten = true
            };
        }
        
        if (resultString == LuaScripts.SetCollisionReturn)
        {
            return new SetResult(key, result)
            {
                ProcessRequirementErrorOccurred = true,
                KeyCollisionOccurred = true
            };
        }

        _logger.LogCritical("Unexpected value returned from Redis set script execution. Expected {Value}. Actual: {Actual}",
            $"{LuaScripts.SuccessReturn}, {LuaScripts.SetCollisionReturn} or {LuaScripts.SetOverwrittenReturn}", resultString);

        return new SetResult(key, result)
        {
            RedisErrorOccurred = true
        };
    }
    
    private void HandleSetResult(string key, RedisResult result)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", result);
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            _logger.LogCritical("Unexpected value type returned from Redis set script execution. Expected a string type. Actual: {Actual}",
                result.Resp3Type.ToString());
        }
        
        if (resultString != LuaScripts.SuccessReturn && resultString != LuaScripts.SetOverwrittenReturn && resultString != LuaScripts.SetCollisionReturn)
        {
            _logger.LogCritical("Unexpected value returned from Redis set script execution. Expected {Value}. Actual: {Actual}",
                $"{LuaScripts.SuccessReturn}, {LuaScripts.SetCollisionReturn} or {LuaScripts.SetOverwrittenReturn}", resultString);
        }
    }

    private RedisValue[] GetSetMembers(byte[] value, SetOptions options, DistributedCacheEntryOptions? entryOptions)
    {
        var creationTime = _timeProvider.GetUtcNow();
        
        var absoluteExpiration = GetAbsoluteExpiration(creationTime, entryOptions ?? options.ExpirationOptions);
        var absoluteExpirationAsUnix = absoluteExpiration?.ToUnixTimeSeconds();
        var slidingExpirationInSeconds = (entryOptions ?? options.ExpirationOptions)?.SlidingExpiration?.TotalSeconds;
        var relativeExpirationInSeconds = GetExpirationInSeconds(creationTime, absoluteExpiration, entryOptions ?? options.ExpirationOptions);
        
        return new RedisValue[]
        {
            absoluteExpirationAsUnix ?? LuaScripts.NotPresent,
            slidingExpirationInSeconds ?? LuaScripts.NotPresent,
            relativeExpirationInSeconds ?? LuaScripts.NotPresent,
            value,
            options.OverwriteIfKeyExists ? LuaScripts.NotPresent : LuaScripts.WithoutKeyOverwriteArg
        };
    }

    private async Task<RedisResult> SetPrivateAsync(string key, byte[] value, SetOptions? options, DistributedCacheEntryOptions? entryOptions, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        var (_, _, redisDatabase) = await ConnectAsync(token);

        var actualOptions = options ?? SetOptions.Default;
        
        var actualKey = Prefix.Append(key);

        var setMembers = GetSetMembers(value, actualOptions, entryOptions);

        return await ProcessAsync(redisDatabase, new []{ actualKey }, setMembers, _knownInternalScripts[nameof(LuaScripts.SetScript)], LuaScripts.SetScript, token);
    }

    private RedisResult SetPrivate(string key, byte[] value, SetOptions? options,
        DistributedCacheEntryOptions? entryOptions)
    {
        var (_, _, redisDatabase) = Connect();

        var actualOptions = options ?? SetOptions.Default;

        var setMembers = GetSetMembers(value, actualOptions, entryOptions);

        var actualKey = Prefix.Append(key);

        return Process(redisDatabase, new []{ actualKey }, setMembers, _knownInternalScripts[nameof(LuaScripts.SetScript)], LuaScripts.SetScript);
    }

    #endregion

    #region Remove

    /// <inheritdoc />
    public IRemoveResult Remove(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = RemovePrivate(key);
        
        return HandleRemoveResultAsExplorerResult(key, result);
    }

    /// <inheritdoc />
    public async Task<IRemoveResult> RemoveAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await RemovePrivateAsync(key, token);
        
        return HandleRemoveResultAsExplorerResult(key, result);
    }

    private void HandleRemoveResult(string key, RedisResult result)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", result);
        }

        if (result.IsNull)
        {
            return;
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            _logger.LogCritical("Unexpected value type returned from Redis remove script execution. Expected a string type. Actual: {Actual}",
                result.Resp3Type.ToString());
        }
        
        if (resultString != LuaScripts.SuccessReturn)
        {
            _logger.LogCritical(
                "Unexpected value returned from Redis remove script execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.SuccessReturn, resultString);
        }
    }
    
    private RemoveResult HandleRemoveResultAsExplorerResult(string key, RedisResult result)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            _logger.LogError("Redis returned an error result - {Error}", result);
            return new RemoveResult(key, result)
            {
                RedisErrorOccurred = true
            };
        }

        if (result.IsNull)
        {
            return new RemoveResult(key, result)
            {
                KeyNotFound = true,
                ProcessRequirementErrorOccurred = true
            };
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            _logger.LogCritical("Unexpected value type returned from Redis remove script execution. Expected a string type. Actual: {Actual}",
                result.Resp3Type.ToString());
            
            return new RemoveResult(key, result)
            {
                RedisErrorOccurred = true
            };
        }
        
        if (resultString != LuaScripts.SuccessReturn)
        {
            _logger.LogCritical(
                "Unexpected value returned from Redis remove script execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.SuccessReturn, resultString);

            return new RemoveResult(key, result)
            {
                RedisErrorOccurred = true
            };
        }
        
        return new RemoveResult(key, result);
    }

    private RedisResult RemovePrivate(string key)
    {
        var (_, _, redisDatabase)  = Connect();

        var actualKey = Prefix.Append(key);

        return Process(redisDatabase, new[] { actualKey }, HashMembersEmpty, _knownInternalScripts[nameof(LuaScripts.RemoveScript)], LuaScripts.RemoveScript);
    }
    
    private async Task<RedisResult> RemovePrivateAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var (_, _, redisDatabase)  = await ConnectAsync(cancellationToken);

        var actualKey = Prefix.Append(key);

        return await ProcessAsync(redisDatabase, new[] { actualKey }, HashMembersEmpty, _knownInternalScripts[nameof(LuaScripts.RemoveScript)], 
            LuaScripts.RemoveScript, cancellationToken);
    }

    #endregion

    private async Task<RedisResult> ProcessAsync(IDatabase redisDatabase, RedisKey[] keys, RedisValue[] values,
        byte[] sha1, string script, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        try
        {
            RedisResult result;
            
            if (ShouldUseSHAOptimization())
            {
                result = await EvalScriptOptimizedAsync(redisDatabase, keys, values, sha1, script, cancellationToken);
            }
            else
            {
                result = await EvalScriptAsync(redisDatabase, keys, values, sha1, script);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ErrorMessage);
            throw;
        }
    }
    
    private RedisResult Process(IDatabase redisDatabase, RedisKey[] keys, RedisValue[] values,
        byte[] sha1, string script)
    {
        try
        {
            RedisResult result;
            if (ShouldUseSHAOptimization())
            {
                result = EvalScriptOptimized(redisDatabase, keys, values, sha1, script);
            }
            else
            {
                result = EvalScript(redisDatabase, keys, values, sha1, script);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ErrorMessage);
            throw;
        }
    }

    private static Task<RedisResult> EvalScriptAsync(IDatabase redisDatabase, RedisKey[] keys, RedisValue[] values,
        byte[] sha1, string script)
        => redisDatabase.ScriptEvaluateAsync(script, keys, values);
    
    private static RedisResult EvalScript(IDatabase redisDatabase, RedisKey[] keys, RedisValue[] values, byte[] sha1, string script)
        => redisDatabase.ScriptEvaluate(script, keys, values);
    
    private static RedisResult EvalScriptOptimized(IDatabase redisDatabase, RedisKey[] keys, RedisValue[] values, byte[] sha1, string script)
    {
        RedisResult result;

        try
        {
            result = redisDatabase.ScriptEvaluate(sha1, keys,
                values);
        }
        catch (RedisServerException redisServerException) when (redisServerException.Message.Contains(NoScript))
        {
            result = redisDatabase.ScriptEvaluate(script, keys,
                values, CommandFlags.NoScriptCache);
        }
        
        return result;
    }
    
    private static async Task<RedisResult> EvalScriptOptimizedAsync(IDatabase redisDatabase, RedisKey[] keys, RedisValue[] values, byte[] sha1, 
        string script, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        RedisResult result;
        
        try
        {
            result = await redisDatabase.ScriptEvaluateAsync(sha1, keys,
                values);
        }
        catch (RedisServerException redisServerException) when (redisServerException.Message.Contains(NoScript))
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            result = await redisDatabase.ScriptEvaluateAsync(script, keys,
                values, CommandFlags.NoScriptCache);
        }

        return result;
    }

    /// <inheritdoc />
    public long? GetExpirationInSeconds(DateTimeOffset creationTime, DateTimeOffset? absoluteExpiration, DistributedCacheEntryOptions? options)
    {
        if (options is null)
        {
            return null;
        }
        
        if (absoluteExpiration.HasValue && options.SlidingExpiration.HasValue)
        {
            return (long)Math.Min(
                (absoluteExpiration.Value - creationTime).TotalSeconds,
                options.SlidingExpiration.Value.TotalSeconds);
        }
        else if (absoluteExpiration.HasValue)
        {
            return (long)(absoluteExpiration.Value - creationTime).TotalSeconds;
        }
        else if (options.SlidingExpiration.HasValue)
        {
            return (long)options.SlidingExpiration.Value.TotalSeconds;
        }
        return null;
    }

    /// <inheritdoc />
    public DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset creationTime, DistributedCacheEntryOptions? options)
    {
        if (options is null)
        {
            return null;
        }
        
        if (options.AbsoluteExpiration.HasValue && options.AbsoluteExpiration <= creationTime)
        {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
            throw new ArgumentOutOfRangeException(
                nameof(DistributedCacheEntryOptions.AbsoluteExpiration),
                options.AbsoluteExpiration.Value,
                "The absolute expiration value must be in the future.");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
        }

        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            return creationTime + options.AbsoluteExpirationRelativeToNow;
        }

        return options.AbsoluteExpiration;
    }
    
    /// <inheritdoc />
    public byte[] SerializeToUtf8Bytes<TValue>(TValue value) where TValue : class
    {
        ArgumentNullException.ThrowIfNull(value);
        
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing the object of type {Type}", typeof(TValue));
            throw;
        }
    }
    
    /// <inheritdoc />
    public TValue Deserialize<TValue>(byte[] bytes) where TValue : class
    {
        ArgumentNullException.ThrowIfNull(bytes);
        
        try
        {
            return JsonSerializer.Deserialize<TValue>(bytes, JsonSerializerOptions) ?? throw new JsonException("The deserialized value is null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing the object of type {Type}", typeof(TValue).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        Interlocked.Exchange(ref _lockFactory, null);
        
        ReleaseConnection(Interlocked.Exchange(ref _multiplexer, null),
            Interlocked.Exchange(ref _lockFactory, null),
            Interlocked.Exchange(ref _redisDatabase, null));
    }
    
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        
        Interlocked.Exchange(ref _lockFactory, null);
        
        return ReleaseConnectionAsync(Interlocked.Exchange(ref _multiplexer, null),
            Interlocked.Exchange(ref _lockFactory, null),
            Interlocked.Exchange(ref _redisDatabase, null));
    }

    private void CheckDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ReleaseConnection(IConnectionMultiplexer? multiplexer, IDistributedLockFactory? lockFactory, IDatabase? database)
    {
        if (multiplexer is null && lockFactory is null) 
            return;
        
        try
        {
            
            if (Options.ConnectionOptions.IsDistributedLockFactoryOwned && lockFactory is not null)
            {
                if (lockFactory is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                // ReSharper disable once SuspiciousTypeConversion.Global
                else if (lockFactory is IAsyncDisposable asyncDisposable)
                {
                    asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            
            if (Options.ConnectionOptions.IsConnectionMultiplexerOwned && multiplexer is not null)
            {
                multiplexer.Close();
                multiplexer.Dispose();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
    
    private async ValueTask ReleaseConnectionAsync(IConnectionMultiplexer? multiplexer, IDistributedLockFactory? lockFactory, IDatabase? database)
    {
        if (multiplexer is null && lockFactory is null) 
            return;
        
        try
        {
            if (Options.ConnectionOptions.IsDistributedLockFactoryOwned && lockFactory is not null)
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (lockFactory is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (lockFactory is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            if (Options.ConnectionOptions.IsConnectionMultiplexerOwned && multiplexer is not null)
            {
                await multiplexer.CloseAsync();
                await multiplexer.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
