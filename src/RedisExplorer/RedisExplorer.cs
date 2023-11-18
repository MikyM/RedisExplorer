using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private static readonly RedisValue[] HashMembersReturnData = { LuaScripts.ReturnDataArg };
    private static readonly RedisValue[] HashMembersDontReturnData = Array.Empty<RedisValue>();
    
    private Dictionary<string,string> _knownInternalScripts = [];
    
    private void PreHashScripts()
    {
        _knownInternalScripts.TryAdd(nameof(LuaScripts.GetAndRefreshScript), CalculateScriptHash(LuaScripts.GetAndRefreshScript));
        _knownInternalScripts.TryAdd(nameof(LuaScripts.RefreshScript), CalculateScriptHash(LuaScripts.RefreshScript));
        _knownInternalScripts.TryAdd(nameof(LuaScripts.SetScript), CalculateScriptHash(LuaScripts.SetScript));
        _knownInternalScripts.TryAdd(nameof(LuaScripts.RemoveScript), CalculateScriptHash(LuaScripts.RemoveScript));
    }
    
    /// <summary>
    /// Json options name.
    /// </summary>
    public const string JsonOptionsName = "RedisExplorerJsonOptions";

    private static RedisValue[] GetHashFields(bool getData) => getData
        ? HashMembersReturnData
        : HashMembersDontReturnData;

    private string GetAndRefreshScript(bool getData, bool forceNonSHA = false)
    {
        string script;
        if (UsingProxy.HasValue && UsingProxy.Value && Options.UseBandwidthOptimizationForProxies && !forceNonSHA)
        {
            script = getData
                ? _knownInternalScripts[nameof(LuaScripts.GetAndRefreshScript)]
                : _knownInternalScripts[nameof(LuaScripts.RefreshScript)];
        }
        else
        {
            script = getData
                ? LuaScripts.GetAndRefreshScript
                : LuaScripts.RefreshScript;
        }

        return script;
    }

    private string GetSetScript(bool forceNonSHA = false)
    {
        string script;
        if (UsingProxy.HasValue && UsingProxy.Value && Options.UseBandwidthOptimizationForProxies && !forceNonSHA)
        {
            script = _knownInternalScripts[nameof(LuaScripts.SetScript)];
        }
        else
        {
            script = LuaScripts.SetScript;
        }

        return script;
    }
    
    private string RemoveScript(bool forceNonSHA = false)
    {
        string script;
        if (UsingProxy.HasValue && UsingProxy.Value && Options.UseBandwidthOptimizationForProxies && !forceNonSHA)
        {
            script = _knownInternalScripts[nameof(LuaScripts.RemoveScript)];
        }
        else
        {
            script = LuaScripts.RemoveScript;
        }

        return script;
    }
    
    private static readonly Version ServerVersionWithExtendedSetCommand = new(4, 0, 0);
    
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
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc/>
    public RedisCacheOptions Options { get; }
    /// <inheritdoc/>
    public JsonSerializerOptions JsonSerializerOptions { get; }
    /// <inheritdoc/>
    public ILogger Logger { get; }
    
    private readonly ILoggerFactory _loggerFactory;

    private readonly SemaphoreSlim _connectionLock = new(initialCount: 1, maxCount: 1);

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor)
        : this(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor,
            NullLoggerFactory.Instance.CreateLogger<RedisExplorer>(), NullLoggerFactory.Instance)
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
        : this(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor,
            NullLoggerFactory.Instance.CreateLogger<RedisExplorer>(), NullLoggerFactory.Instance)
    {
        ConfigurationOptions = configurationOptions;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="configurationOptions">The Redis configuration options.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor,
        ILogger logger, ILoggerFactory loggerFactory, ConfigurationOptions configurationOptions)
        : this(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor, logger, loggerFactory)
    {
        ConfigurationOptions = configurationOptions;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor,
        ILogger logger, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        ArgumentNullException.ThrowIfNull(jsonSerializerOptionsAccessor);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        TimeProvider = timeProvider;
        Options = optionsAccessor.Value;
        JsonSerializerOptions = jsonSerializerOptionsAccessor.Get(JsonOptionsName);
        Logger = logger;
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
    public static string CalculateScriptHash(string script)
    {
        var scriptBytes = Encoding.ASCII.GetBytes(script);
        var hashBytes = SHA1.HashData(scriptBytes);
        var hashString = BitConverter.ToString(hashBytes).Replace("-", "");
        return hashString;
    }
    
    /// <inheritdoc/>
    public IDatabase GetDatabase()
    {
        CheckDisposed();

        var connect = Connect();

        return connect.RedisDatabase;
    }
    
    /// <inheritdoc/>
    public async Task<IDatabase> GetDatabaseAsync()
    {
        CheckDisposed();

        var connect = await ConnectAsync();

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
    public async Task<IConnectionMultiplexer> GetMultiplexerAsync()
    {
        CheckDisposed();

        var connect = await ConnectAsync();

        return connect.Multiplexer;
    }

    /// <inheritdoc/>
    public IDistributedLockFactory GetLockFactory()
    {
        CheckDisposed();

        var connect = Connect();

        return connect.LockFactory;
    }

    /// <inheritdoc/>
    public async Task<IDistributedLockFactory> GetLockFactoryAsync()
    {
        CheckDisposed();

        var connect = await ConnectAsync();

        return connect.LockFactory;
    }
    
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

    /// <inheritdoc />
    public string GetPrefixedKey(string key)
    {
        CheckDisposed();
        return $"{Options.Prefix}{key}";
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
    
    /// <inheritdoc />
    byte[]? IDistributedCache.Get(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        
        var result = GetAndRefresh(key, true);

        return HandleGetAndRefreshResult(key, result, true);
    }

    /// <inheritdoc />
    async Task<byte[]?> IDistributedCache.GetAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await GetAndRefreshAsync(key, true, token);

        return HandleGetAndRefreshResult(key, result, true);
    }
    
    /// <inheritdoc />
    public IExplorerResult<byte[]> Get(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = GetAndRefresh(key, true);

        return HandleGetAndRefreshResultAsExplorerResult(key, result, true);
    }

    /// <inheritdoc />
    public async Task<IExplorerResult<byte[]>> GetAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await GetAndRefreshAsync(key, true, token);

        return HandleGetAndRefreshResultAsExplorerResult(key, result, true);
    }
    
    /// <inheritdoc />
    void IDistributedCache.Refresh(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        
        var result = GetAndRefresh(key, false);

        HandleGetAndRefreshResult(key, result, false);
    }

    /// <inheritdoc />
    async Task IDistributedCache.RefreshAsync(string key, CancellationToken token)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        token.ThrowIfCancellationRequested();

        var result = await GetAndRefreshAsync(key, false, token);

        HandleGetAndRefreshResult(key, result, false);
    }
    
    /// <inheritdoc />
    public IExplorerResult Refresh(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        
        var result = GetAndRefresh(key, false);

        return HandleGetAndRefreshResultAsExplorerResult(key, result, false);
    }

    /// <inheritdoc />
    public async Task<IExplorerResult> RefreshAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        token.ThrowIfCancellationRequested();

        var result = await GetAndRefreshAsync(key, false, token);

        return HandleGetAndRefreshResultAsExplorerResult(key, result, false);
    }
    
    
    private RedisResult GetAndRefresh(string key, bool getData)
    {
        var (_, _, redisDatabase) = Connect();

        var actualKey = Prefix.Append(key);
        
        // This also resets the LRU status as desired.
        // Calculations regarding expiration done server side.
        var result = redisDatabase.ScriptEvaluate(GetAndRefreshScript(getData),
            new[] { actualKey },
            GetHashFields(getData));

        // this probably means the script was not loaded yet, lets retry with full script
        if (ShouldRetryDueToSHAOptimization(result))
        {
            result = redisDatabase.ScriptEvaluate(GetAndRefreshScript(getData, true),
                new[] { actualKey },
                GetHashFields(getData));
        }

        return result;
    }
    
    private byte[]? HandleGetAndRefreshResult(string key,  RedisResult result, bool getData)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            Logger.LogError("Redis returned an error result - {Error}", (string?)result);
            return null;
        }
        
        if (result.IsNull)
            return null;

        if (getData)
        {
            return (byte[]?)result;
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            Logger.LogWarning("Unexpected value type returned from Redis refresh script execution. Expected a string type. Actual: {ActualType}",
                result.Resp3Type.ToString());

            return null;
        }
            
        if (resultString != LuaScripts.NoDataReturnedSuccessValue)
        {
            Logger.LogWarning(
                "Unexpected value returned from Redis script refresh execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.NoDataReturnedSuccessValue, resultString);
        }

        return null;
    }

    private ExplorerResult<byte[]> HandleGetAndRefreshResultAsExplorerResult(string key,  RedisResult result, bool getData)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            Logger.LogError("Redis returned an error result - {Error}", (string?)result);
            return new ExplorerResult<byte[]>(key, result, ExplorerResultFlags.UnknownErrorOccurred);
        }

        if (result.IsNull)
        {
            return new ExplorerResult<byte[]>(key, result, ExplorerResultFlags.KeyNotFound);
        }

        if (getData)
        {
            return new ExplorerResult<byte[]>(key, result, ExplorerResultFlags.Success, (byte[]?)result);
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            Logger.LogWarning("Unexpected value type returned from Redis refresh script execution. Expected a string type. Actual: {ActualType}",
                result.Resp3Type.ToString());

            return new ExplorerResult<byte[]>(key, result, ExplorerResultFlags.UnexpectedResultAcquired);
        }
            
        if (resultString != LuaScripts.NoDataReturnedSuccessValue)
        {
            Logger.LogWarning(
                "Unexpected value returned from Redis refresh script execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.NoDataReturnedSuccessValue, resultString);
            
            return new ExplorerResult<byte[]>(key, result, ExplorerResultFlags.UnexpectedResultAcquired);
        }

        return new ExplorerResult<byte[]>(key, result, ExplorerResultFlags.Success);
    }
    
    private async Task<RedisResult> GetAndRefreshAsync(string key, bool getData, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        var (_, _, redisDatabase) = await ConnectAsync(token);

        var actualKey = Prefix.Append(key);
        
        // This also resets the LRU status as desired.
        // Calculations regarding expiration done server side.
        var result = await redisDatabase.ScriptEvaluateAsync(GetAndRefreshScript(getData),
            new[] { actualKey },
            GetHashFields(getData));
        
        // this probably means the script was not loaded yet, lets retry with full script
        if (ShouldRetryDueToSHAOptimization(result))
        {
            result = await redisDatabase.ScriptEvaluateAsync(GetAndRefreshScript(getData, true),
                new[] { actualKey },
                GetHashFields(getData));
        }

        return result;
    }

    /// <inheritdoc />
    void IDistributedCache.Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var result = SetPrivate(key, value, options);

        HandleSetResult(key, result);
    }
    
    /// <inheritdoc />
    async Task IDistributedCache.SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var result = await SetPrivateAsync(key, value, options, token);

        HandleSetResult(key, result);
    }

    private ExplorerResult HandleSetResultAsExplorerResult(string key, RedisResult result)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            Logger.LogError("Redis returned an error result - {Error}", result);
            return new ExplorerResult(key, result, ExplorerResultFlags.UnknownErrorOccurred);
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            Logger.LogWarning("Unexpected value type returned from Redis set script execution. Expected a string type. Actual: {Actual}",
                result.Resp3Type.ToString());
            
            return new ExplorerResult(key, result, ExplorerResultFlags.UnexpectedResultAcquired);
        }
        
        if (resultString != LuaScripts.NoDataReturnedSuccessValue && resultString != LuaScripts.NoDataReturnedSuccessSetOverwrittenValue)
        {
            Logger.LogWarning("Unexpected value returned from Redis set script execution. Expected {Value}. Actual: {Actual}",
                $"{LuaScripts.NoDataReturnedSuccessValue} or {LuaScripts.NoDataReturnedSuccessSetOverwrittenValue}", resultString);
            
            return new ExplorerResult(key, result, ExplorerResultFlags.UnexpectedResultAcquired);
        }

        if (resultString == LuaScripts.NoDataReturnedSuccessSetOverwrittenValue)
        {
            return new ExplorerResult(key, result, ExplorerResultFlags.Success | ExplorerResultFlags.KeyOverwritten);
        }
        
        return new ExplorerResult(key, result, ExplorerResultFlags.Success);
    }
    
    private void HandleSetResult(string key, RedisResult result)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            Logger.LogError("Redis returned an error result - {Error}", result);
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            Logger.LogWarning("Unexpected value type returned from Redis set script execution. Expected a string type. Actual: {Actual}",
                result.Resp3Type.ToString());
        }
        
        if (resultString != LuaScripts.NoDataReturnedSuccessValue && resultString != LuaScripts.NoDataReturnedSuccessSetOverwrittenValue)
        {
            Logger.LogWarning("Unexpected value returned from Redis set script execution. Expected {Value}. Actual: {Actual}",
                $"{LuaScripts.NoDataReturnedSuccessValue} or {LuaScripts.NoDataReturnedSuccessSetOverwrittenValue}", resultString);
        }
    }
    
    /// <inheritdoc />
    public IExplorerResult Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var result = SetPrivate(key, value, options);

        return HandleSetResultAsExplorerResult(key, result);
    }

    /// <inheritdoc />
    public async Task<IExplorerResult> SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var result = await SetPrivateAsync(key, value, options, token);

        return HandleSetResultAsExplorerResult(key, result);
    }

    private async Task<RedisResult> SetPrivateAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        var (_, _, redisDatabase) = await ConnectAsync(token);

        var creationTime = TimeProvider.GetUtcNow();

        var absoluteExpiration = GetAbsoluteExpiration(creationTime, options);
        var absoluteExpirationAsUnix = absoluteExpiration?.ToUnixTimeSeconds();
        var slidingExpirationInSeconds = options.SlidingExpiration?.TotalSeconds;
        var relativeExpirationInSeconds = GetExpirationInSeconds(creationTime, absoluteExpiration, options);

        var actualKey = Prefix.Append(key);
        
        var result = await redisDatabase.ScriptEvaluateAsync(GetSetScript(), new[] { Prefix.Append(key) },
            new RedisValue[]
            {
                absoluteExpirationAsUnix ?? LuaScripts.NotPresent,
                slidingExpirationInSeconds ?? LuaScripts.NotPresent,
                relativeExpirationInSeconds ?? LuaScripts.NotPresent,
                value
            });
        
        // this probably means the script was not loaded yet, lets retry with full script
        if (ShouldRetryDueToSHAOptimization(result))
        {
            result = await redisDatabase.ScriptEvaluateAsync(GetSetScript(true), new[] { actualKey },
                new RedisValue[]
                {
                    absoluteExpirationAsUnix ?? LuaScripts.NotPresent,
                    slidingExpirationInSeconds ?? LuaScripts.NotPresent,
                    relativeExpirationInSeconds ?? LuaScripts.NotPresent,
                    value
                });
        }

        return result;
    }

    private RedisResult SetPrivate(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var (_, _, redisDatabase) = Connect();

        var creationTime = TimeProvider.GetUtcNow();

        var absoluteExpiration = GetAbsoluteExpiration(creationTime, options);
        var absoluteExpirationAsUnix = absoluteExpiration?.ToUnixTimeSeconds();
        var slidingExpirationInSeconds = options.SlidingExpiration?.TotalSeconds;
        var relativeExpirationInSeconds = GetExpirationInSeconds(creationTime, absoluteExpiration, options);

        var actualKey = Prefix.Append(key);

        var result = redisDatabase.ScriptEvaluate(GetSetScript(), new[] { actualKey },
            new RedisValue[]
            {
                absoluteExpirationAsUnix ?? LuaScripts.NotPresent,
                slidingExpirationInSeconds ?? LuaScripts.NotPresent,
                relativeExpirationInSeconds ?? LuaScripts.NotPresent,
                value
            });
        
        // this probably means the script was not loaded yet, lets retry with full script
        if (ShouldRetryDueToSHAOptimization(result))
        {
            result = redisDatabase.ScriptEvaluate(GetSetScript(true), new[] { actualKey },
                new RedisValue[]
                {
                    absoluteExpirationAsUnix ?? LuaScripts.NotPresent,
                    slidingExpirationInSeconds ?? LuaScripts.NotPresent,
                    relativeExpirationInSeconds ?? LuaScripts.NotPresent,
                    value
                });
        }

        return result;
    }

    private bool ShouldRetryDueToSHAOptimization(RedisResult result)
        => UsingProxy.HasValue && UsingProxy.Value && Options.UseBandwidthOptimizationForProxies &&
           result.Resp3Type == ResultType.Error;

    /// <inheritdoc />
    public long? GetExpirationInSeconds(DateTimeOffset creationTime, DateTimeOffset? absoluteExpiration, DistributedCacheEntryOptions options)
    {
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
    public DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset creationTime, DistributedCacheEntryOptions options)
    {
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
    void IDistributedCache.Remove(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = RemovePrivate(key);
        
        HandleRemoveResult(key, result);
    }

    /// <inheritdoc />
    async Task IDistributedCache.RemoveAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = await RemovePrivateAsync(key, token);
        
        HandleRemoveResult(key, result);
    }

    /// <inheritdoc />
    public IExplorerResult Remove(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var result = RemovePrivate(key);
        
        return HandleRemoveResultAsExplorerResult(key, result);
    }

    /// <inheritdoc />
    public async Task<IExplorerResult> RemoveAsync(string key, CancellationToken token = default)
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
            Logger.LogError("Redis returned an error result - {Error}", result);
        }

        if (result.IsNull)
        {
            return;
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            Logger.LogWarning("Unexpected value type returned from Redis remove script execution. Expected a string type. Actual: {Actual}",
                result.Resp3Type.ToString());
        }
        
        if (resultString != LuaScripts.NoDataReturnedSuccessValue)
        {
            Logger.LogWarning(
                "Unexpected value returned from Redis remove script execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.NoDataReturnedSuccessValue, resultString);
        }
    }
    
    private ExplorerResult HandleRemoveResultAsExplorerResult(string key, RedisResult result)
    {
        if (result.Resp3Type is ResultType.Error or ResultType.BlobError)
        {
            Logger.LogError("Redis returned an error result - {Error}", result);
            return new ExplorerResult(key, result, ExplorerResultFlags.UnknownErrorOccurred);
        }

        if (result.IsNull)
        {
            return new ExplorerResult(key, result, ExplorerResultFlags.KeyNotFound);
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            Logger.LogWarning("Unexpected value type returned from Redis remove script execution. Expected a string type. Actual: {Actual}",
                result.Resp3Type.ToString());
            
            return new ExplorerResult(key, result, ExplorerResultFlags.UnexpectedResultAcquired);
        }
        
        if (resultString != LuaScripts.NoDataReturnedSuccessValue)
        {
            Logger.LogWarning(
                "Unexpected value returned from Redis remove script execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.NoDataReturnedSuccessValue, resultString);
            
            return new ExplorerResult(key, result, ExplorerResultFlags.UnexpectedResultAcquired);
        }
        
        return new ExplorerResult(key, result, ExplorerResultFlags.Success);
    }

    private RedisResult RemovePrivate(string key)
    {
        var (_, _, redisDatabase)  = Connect();

        var actualKey = Prefix.Append(key);

        var result = redisDatabase.ScriptEvaluate(RemoveScript(), new[] { actualKey },
            HashMembersDontReturnData);
        
        // this probably means the script was not loaded yet, lets retry with full script
        if (ShouldRetryDueToSHAOptimization(result))
        {
            result = redisDatabase.ScriptEvaluate(RemoveScript(true), new[] { actualKey },
                HashMembersDontReturnData);
        }
        
        return result;
    }
    
    private async Task<RedisResult> RemovePrivateAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var (_, _, redisDatabase)  = await ConnectAsync(cancellationToken);

        var actualKey = Prefix.Append(key);

        var result = await redisDatabase.ScriptEvaluateAsync(RemoveScript(), new[] { actualKey },
            HashMembersDontReturnData);
        
        // this probably means the script was not loaded yet, lets retry with full script
        if (ShouldRetryDueToSHAOptimization(result))
        {
            result = await redisDatabase.ScriptEvaluateAsync(RemoveScript(true), new[] { actualKey },
                HashMembersDontReturnData);
        }

        return result;
    }
    
    /// <inheritdoc />
    public Task<IExplorerResult> SetAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options,
        CancellationToken token = default) where TValue : class
    {
        var serializedValue = SerializeToUtf8Bytes(value);
        return SetAsync(key, serializedValue, options, token);
    }
    
    /// <inheritdoc />
    public Task<IExplorerResult> SetAsync<TValue>(string key, TValue value, CancellationToken token = default) where TValue : class
        => SetAsync(key, value, Options.ExpirationOptions.GetEntryOptions<TValue>(), token);
        
    /// <inheritdoc />
    public IExplorerResult Set<TValue>(string key, TValue value, DistributedCacheEntryOptions options) where TValue : class
    {
        var serializedValue = SerializeToUtf8Bytes(value);
        return Set(key, serializedValue, options);
    }
    
    /// <inheritdoc />
    public IExplorerResult Set<TValue>(string key, TValue value) where TValue : class
        => Set(key, value, Options.ExpirationOptions.GetEntryOptions<TValue>());
    
    /// <inheritdoc />
    public async Task<IExplorerResult<TValue>> GetAsync<TValue>(string key, CancellationToken token = default) where TValue : class
    {
        var value = await GetAsync(key, token);

        if (value.IsDefined(out var bytes))
        {
            return new ExplorerResult<TValue>(key, value.RedisResult, value.Flags, Deserialize<TValue>(bytes));
        }
        
        return new ExplorerResult<TValue>(key, value.RedisResult, value.Flags);
    }
    
    /// <inheritdoc />
    public IExplorerResult<TValue> Get<TValue>(string key) where TValue : class
    {
        var value = Get(key);

        if (value.IsDefined(out var bytes))
        {
            return new ExplorerResult<TValue>(key, value.RedisResult, value.Flags, Deserialize<TValue>(bytes));
        }
        
        return new ExplorerResult<TValue>(key, value.RedisResult, value.Flags);
    }
    
    /// <inheritdoc />
    public byte[] SerializeToUtf8Bytes<TValue>(TValue value) where TValue : class
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptions);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error serializing the object of type {Type}", typeof(TValue));
            throw;
        }
    }
    
    /// <inheritdoc />
    public TValue Deserialize<TValue>(byte[] bytes) where TValue : class
    {
        try
        {
            return JsonSerializer.Deserialize<TValue>(bytes, JsonSerializerOptions) ?? throw new JsonException("The deserialized value is null.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deserializing the object of type {Type}", typeof(TValue).Name);
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
