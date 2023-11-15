using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RedisExplorer;

/// <summary>
/// Distributed cache implementation using Redis.
/// <para>Uses <c>StackExchange.Redis</c> as the Redis client.</para>
/// </summary>
[PublicAPI]
public sealed class RedisExplorer : IRedisExplorer, IDisposable, IAsyncDisposable
{
    // Note that the "force reconnect" pattern as described https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/cache-best-practices-connection#using-forcereconnect-with-stackexchangeredis
    // can be enabled via the "Microsoft.AspNetCore.Caching.StackExchangeRedis.UseForceReconnect" app-context switch


    // combined keys - same hash keys fetched constantly; avoid allocating an array each time
    private static readonly RedisValue[] HashMembersReturnData = { LuaScripts.ReturnDataArg };
    private static readonly RedisValue[] HashMembersDontReturnData = Array.Empty<RedisValue>();
    
    /// <summary>
    /// Json options name.
    /// </summary>
    public const string JsonOptionsName = "RedisExplorerJsonOptions";

    private static RedisValue[] GetHashFields(bool getData) => getData
        ? HashMembersReturnData
        : HashMembersDontReturnData;

    private static string GetAndRefreshLuaScript(bool getData) => getData
        ? LuaScripts.GetAndRefreshScript
        : LuaScripts.RefreshScript;
    
    private static readonly Version ServerVersionWithExtendedSetCommand = new(4, 0, 0);
    
    private volatile IDistributedLockFactory? _lockFactory;
    private volatile IConnectionMultiplexer? _multiplexer;
    private volatile IDatabase? _redisDatabase;
    
    private bool _disposed;
    private string _setScript = LuaScripts.SetScript;

    private readonly RedisCacheOptions _options;
    private readonly ImmutableRedisExplorerOptions _redisExplorerOptions;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    
    private readonly RedisKey _prefix;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly TimeProvider _timeProvider;

    private readonly SemaphoreSlim _connectionLock = new(initialCount: 1, maxCount: 1);

    private long _lastConnectTicks;
    private long _firstErrorTimeTicks;
    private long _previousErrorTimeTicks;

    // StackExchange.Redis will also be trying to reconnect internally,
    // so limit how often we recreate the ConnectionMultiplexer instance
    // in an attempt to reconnect

    // Never reconnect within 60 seconds of the last attempt to connect or reconnect.
    private readonly TimeSpan _reconnectMinInterval = TimeSpan.FromSeconds(60);
    // Only reconnect if errors have occurred for at least the last 30 seconds.
    // This count resets if there are no errors for 30 seconds
    private readonly TimeSpan _reconnectErrorThreshold = TimeSpan.FromSeconds(30);

    private static DateTimeOffset ReadTimeTicks(ref long field)
    {
        var ticks = Volatile.Read(ref field); // avoid torn values
        return ticks == 0 ? DateTimeOffset.MinValue : new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static void WriteTimeTicks(ref long field, DateTimeOffset value)
    {
        var ticks = value == DateTimeOffset.MinValue ? 0L : value.UtcTicks;
        Volatile.Write(ref field, ticks); // avoid torn values
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="redisExplorerOptions">Cache settings.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor, ImmutableRedisExplorerOptions redisExplorerOptions)
        : this(timeProvider, optionsAccessor, jsonSerializerOptionsAccessor,
            NullLoggerFactory.Instance.CreateLogger<RedisExplorer>(), NullLoggerFactory.Instance, redisExplorerOptions)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RedisExplorer"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="optionsAccessor">The configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="jsonSerializerOptionsAccessor">The serialization options.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="redisExplorerOptions">Cache settings.</param>
    public RedisExplorer(TimeProvider timeProvider, IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptionsAccessor,
        ILogger logger, ILoggerFactory loggerFactory, ImmutableRedisExplorerOptions redisExplorerOptions)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        ArgumentNullException.ThrowIfNull(jsonSerializerOptionsAccessor);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(redisExplorerOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = optionsAccessor.Value;
        _jsonSerializerOptions = jsonSerializerOptionsAccessor.Get(JsonOptionsName);
        _logger = logger;
        _loggerFactory = loggerFactory;
        _redisExplorerOptions = redisExplorerOptions;
        _timeProvider = timeProvider;

        _lastConnectTicks = _timeProvider.GetUtcNow().Ticks;

        // This allows partitioning a single backend cache for use with multiple apps/services.
        var prefix = _options.Prefix;
        if (!string.IsNullOrEmpty(prefix))
        {
            // SE.Redis allows efficient append of key-prefix scenarios, but we can help it
            // avoid some work/allocations by forcing the key-prefix to be a byte[]; SE.Redis
            // would do this itself anyway, using UTF8
            _prefix = (RedisKey)Encoding.UTF8.GetBytes(prefix);
        }
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
    public IDistributedLock CreateLock(string resource, TimeSpan expiryTime)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(resource);
        
        var (multiplexer, lockFactory, redisDatabase) = Connect();

        try
        {
            return lockFactory.CreateLock(resource, expiryTime);
        }
        catch (Exception ex)
        {
            OnRedisError(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }
    }

    /// <inheritdoc cref="IRedisExplorer.CreateLockAsync(string, TimeSpan, CancellationToken)"/>
    public async Task<IDistributedLock> CreateLockAsync(string resource, TimeSpan expiryTime, CancellationToken cancellationToken = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(resource);

        var (multiplexer, lockFactory, redisDatabase) = await ConnectAsync(cancellationToken);
        
        try
        {
            return (await lockFactory.CreateLockAsync(resource, expiryTime, cancellationToken));
        }
        catch (Exception ex)
        {
            await OnRedisErrorAsync(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }
    }
    

    /// <inheritdoc cref="IRedisExplorer.CreateLock(string, TimeSpan, TimeSpan, TimeSpan, CancellationToken)"/>
    public IDistributedLock CreateLock(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime, CancellationToken cancellationToken = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(resource);
        
        var (multiplexer, lockFactory, redisDatabase) = Connect();

        try
        {
            return lockFactory.CreateLock(resource, expiryTime, waitTime, retryTime, cancellationToken);
        }
        catch (Exception ex)
        {
            OnRedisError(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }
    }

    /// <inheritdoc cref="IRedisExplorer.CreateLockAsync(string, TimeSpan, TimeSpan, TimeSpan, CancellationToken)"/>
    public async Task<IDistributedLock> CreateLockAsync(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime,
        CancellationToken cancellationToken = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(resource);

        var (multiplexer, lockFactory, redisDatabase) = await ConnectAsync(cancellationToken);
        
        try
        {
            return (await lockFactory.CreateLockAsync(resource, expiryTime, waitTime, retryTime, cancellationToken));
        }
        catch (Exception ex)
        {
            await OnRedisErrorAsync(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }
    }

    /// <inheritdoc />
    public string GetPrefixedKey(string key)
    {
        CheckDisposed();
        return $"{_options.Prefix}{key}";
    }

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        return GetAndRefresh(key, getData: true);
    }

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        token.ThrowIfCancellationRequested();

        return GetAndRefreshAsync(key, getData: true, token: token);
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var (multiplexer, lockFactory, redisDatabase) = Connect();

        var creationTime = _timeProvider.GetUtcNow();

        var absoluteExpiration = GetAbsoluteExpiration(creationTime, options);

        try
        {
            redisDatabase.ScriptEvaluate(_setScript, new[] { _prefix.Append(key) },
                new RedisValue[]
                {
                        absoluteExpiration?.ToUnixTimeSeconds() ?? LuaScripts.NotPresent,
                        options.SlidingExpiration?.TotalSeconds ?? LuaScripts.NotPresent,
                        GetExpirationInSeconds(creationTime, absoluteExpiration, options) ?? LuaScripts.NotPresent,
                        value
                });
        }
        catch (Exception ex)
        {
            OnRedisError(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        token.ThrowIfCancellationRequested();

        var (multiplexer, lockFactory, redisDatabase) = await ConnectAsync(token);

        var creationTime = _timeProvider.GetUtcNow();

        var absoluteExpiration = GetAbsoluteExpiration(creationTime, options);

        try
        {
            await redisDatabase.ScriptEvaluateAsync(_setScript, new[] { _prefix.Append(key) },
                new RedisValue[]
                {
                absoluteExpiration?.ToUnixTimeSeconds() ?? LuaScripts.NotPresent,
                options.SlidingExpiration?.TotalSeconds ?? LuaScripts.NotPresent,
                GetExpirationInSeconds(creationTime, absoluteExpiration, options) ?? LuaScripts.NotPresent,
                value
                });
        }
        catch (Exception ex)
        {
            await OnRedisErrorAsync(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        CheckDisposed();
        ArgumentNullException.ThrowIfNull(key);
        GetAndRefresh(key, getData: false);
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        token.ThrowIfCancellationRequested();

        await GetAndRefreshAsync(key, getData: false, token: token);
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
                
                return new (multiplexer, lockFactory, redisDatabase);
            }
            
            IConnectionMultiplexer connection;
                
            if (_options.ConnectionMultiplexerFactoryOptions is null)
            {
                connection = ConnectionMultiplexer.Connect(_options.GetConfiguredOptions("RedisExplorer"));
            }
            else
            {
                connection = _options.ConnectionMultiplexerFactoryOptions.Factory().GetAwaiter().GetResult();
            }

            PrepareConnection(connection);

            multiplexer = connection;
                
            _ = Interlocked.Exchange(ref _multiplexer, multiplexer);

            redisDatabase = multiplexer.GetDatabase();

            _ = Interlocked.Exchange(ref _redisDatabase, redisDatabase);
                
            lockFactory = _options.DistributedLockFactory(multiplexer, _loggerFactory).GetAwaiter().GetResult();
                
            _ = Interlocked.Exchange(ref _lockFactory, lockFactory);
            
            Debug.Assert(_lockFactory is not null);
            Debug.Assert(_multiplexer is not null);
            Debug.Assert(_redisDatabase is not null);
            
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
            
            return ValueTask.FromResult<(IConnectionMultiplexer Multiplexer, IDistributedLockFactory LockFactory, IDatabase RedisDatabase)>(new (multiplexer, lockFactory, redisDatabase));
        }
        
        return ConnectSlowAsync(token);
    }

    [MemberNotNull(nameof(_lockFactory), nameof(_multiplexer), nameof(_redisDatabase))]
    private async ValueTask<(IConnectionMultiplexer Multiplexer, IDistributedLockFactory LockFactory, IDatabase RedisDatabase)> ConnectSlowAsync(CancellationToken token)
    {
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
                
                return new (multiplexer, lockFactory, redisDatabase);
            }
            
            IConnectionMultiplexer connection;
                
            if (_options.ConnectionMultiplexerFactoryOptions is null)
            {
                connection = await ConnectionMultiplexer.ConnectAsync(_options.GetConfiguredOptions("RedisExplorer"));
            }
            else
            {
                connection = await _options.ConnectionMultiplexerFactoryOptions.Factory();
            }

            PrepareConnection(connection);

            multiplexer = connection;
                
            _ = Interlocked.Exchange(ref _multiplexer, multiplexer);
                
            redisDatabase = multiplexer.GetDatabase();

            _ = Interlocked.Exchange(ref _redisDatabase, redisDatabase);
            
            lockFactory = await _options.DistributedLockFactory(multiplexer, _loggerFactory);
                
            _ = Interlocked.Exchange(ref _lockFactory, lockFactory);
            
            Debug.Assert(_lockFactory is not null);
            Debug.Assert(_multiplexer is not null);
            Debug.Assert(_redisDatabase is not null);
            
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
        WriteTimeTicks(ref _lastConnectTicks, _timeProvider.GetUtcNow());
        ValidateServerFeatures(connection);
        TryRegisterProfiler(connection);
    }

    private void ValidateServerFeatures(IConnectionMultiplexer connection)
    {
        CheckDisposed();
        
        _ = connection ?? throw new InvalidOperationException($"{nameof(connection)} cannot be null.");

        try
        {
            foreach (var endPoint in connection.GetEndPoints())
            {
                if (connection.GetServer(endPoint).Version < ServerVersionWithExtendedSetCommand)
                {
                    _setScript = LuaScripts.SetScriptPreExtendedSetCommand;
                    return;
                }
            }
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex,"Could not determine the Redis server version. Falling back to use HMSET command instead of HSET");

            // The GetServer call may not be supported with some configurations, in which
            // case let's also fall back to using the older command.
            _setScript = LuaScripts.SetScriptPreExtendedSetCommand;
        }
    }

    private void TryRegisterProfiler(IConnectionMultiplexer connection)
    {
        CheckDisposed();
        
        _ = connection ?? throw new InvalidOperationException($"{nameof(connection)} cannot be null.");

        if (_options.ProfilingSession is not null)
        {
            connection.RegisterProfiler(_options.ProfilingSession);
        }
    }

    private byte[]? GetAndRefresh(string key, bool getData)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var (multiplexer, lockFactory, redisDatabase) = Connect();

        // This also resets the LRU status as desired.
        // Calculations regarding expiration done server side.
        RedisResult result;
        try
        {
            result = redisDatabase.ScriptEvaluate(GetAndRefreshLuaScript(getData),
                    new[] { _prefix.Append(key) },
                    GetHashFields(getData));
        }
        catch (Exception ex)
        {
            OnRedisError(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }

        if (result.IsNull)
            return null;

        if (getData)
        {
            return (byte[]?)result;
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            _logger.LogWarning("Unexpected value returned from Redis script execution. Expected: {ExpectedType}. Actual: {ActualType}",
                $"{ResultType.SimpleString} or {ResultType.BulkString}", result.Resp3Type.ToString());

            return null;
        }
            
        if (resultString != LuaScripts.SuccessfulScriptNoDataReturnedValue)
        {
            _logger.LogWarning(
                "Unexpected value returned from Redis script execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.SuccessfulScriptNoDataReturnedValue, resultString);
        }

        return null;
    }

    private async Task<byte[]?> GetAndRefreshAsync(string key, bool getData, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        token.ThrowIfCancellationRequested();

        var (multiplexer, lockFactory, redisDatabase) = await ConnectAsync(token);

        // This also resets the LRU status as desired.
        // Calculations regarding expiration done server side.
        RedisResult result;
        try
        {
            result = await redisDatabase.ScriptEvaluateAsync(GetAndRefreshLuaScript(getData),
                    new[] { _prefix.Append(key) },
                    GetHashFields(getData));
        }
        catch (Exception ex)
        {
            await OnRedisErrorAsync(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }

        if (result.IsNull)
            return null;

        if (getData)
        {
            return (byte[]?)result;
        }

        if (!result.TryExtractString(out var resultString, out _, out _))
        {
            _logger.LogWarning("Unexpected value returned from Redis script execution. Expected: {ExpectedType}. Actual: {ActualType}",
                $"{ResultType.SimpleString} or {ResultType.BulkString}", result.Resp3Type.ToString());

            return null;
        }
            
        if (resultString != LuaScripts.SuccessfulScriptNoDataReturnedValue)
        {
            _logger.LogWarning(
                "Unexpected value returned from Redis script execution. Expected: {ExpectedValue}. Actual: {ActualValue}", 
                LuaScripts.SuccessfulScriptNoDataReturnedValue, resultString);
        }

        return null;
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var (multiplexer, lockFactory, redisDatabase)  = Connect();
        try
        {
            redisDatabase.KeyDelete(_prefix.Append(key));
        }
        catch (Exception ex)
        {
            OnRedisError(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        CheckDisposed();
        
        ArgumentNullException.ThrowIfNull(key);

        var (multiplexer, lockFactory, redisDatabase) = await ConnectAsync(token);

        try
        {
            await redisDatabase.KeyDeleteAsync(_prefix.Append(key));
        }
        catch (Exception ex)
        {
            await OnRedisErrorAsync(ex, multiplexer, lockFactory, redisDatabase);
            throw;
        }
    }

    private static long? GetExpirationInSeconds(DateTimeOffset creationTime, DateTimeOffset? absoluteExpiration, DistributedCacheEntryOptions options)
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

    private static DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset creationTime, DistributedCacheEntryOptions options)
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

    private bool ShouldForceReconnect()
    {
        var utcNow = _timeProvider.GetUtcNow();
        var previousConnectTime = ReadTimeTicks(ref _lastConnectTicks);
        var elapsedSinceLastReconnect = utcNow - previousConnectTime;

        // We want to limit how often we perform this top-level reconnect, so we check how long it's been since our last attempt.
        if (elapsedSinceLastReconnect < _reconnectMinInterval)
        {
            return false;
        }

        var firstErrorTime = ReadTimeTicks(ref _firstErrorTimeTicks);
        if (firstErrorTime == DateTimeOffset.MinValue)
        {
            // note: order/timing here (between the two fields) is not critical
            WriteTimeTicks(ref _firstErrorTimeTicks, utcNow);
            WriteTimeTicks(ref _previousErrorTimeTicks, utcNow);
            return false;
        }

        var elapsedSinceFirstError = utcNow - firstErrorTime;
        var elapsedSinceMostRecentError = utcNow - ReadTimeTicks(ref _previousErrorTimeTicks);

        var shouldReconnect =
            elapsedSinceFirstError >=
            _reconnectErrorThreshold // Make sure we gave the multiplexer enough time to reconnect on its own if it could.
            && elapsedSinceMostRecentError <=
            _reconnectErrorThreshold; // Make sure we aren't working on stale data (e.g. if there was a gap in errors, don't reconnect yet).

        // Update the previousErrorTime timestamp to be now (e.g. this reconnect request).
        WriteTimeTicks(ref _previousErrorTimeTicks, utcNow);

        if (!shouldReconnect)
        {
            return false;
        }

        WriteTimeTicks(ref _firstErrorTimeTicks, DateTimeOffset.MinValue);
        WriteTimeTicks(ref _previousErrorTimeTicks, DateTimeOffset.MinValue);

        return true;
    }

    private void OnRedisError(Exception exception, IConnectionMultiplexer multiplexer, IDistributedLockFactory lockFactory, IDatabase database)
    {
        if (_options.UseForceReconnect && (exception is RedisConnectionException or SocketException))
        {
            var shouldReconnect = ShouldForceReconnect();
            if (!shouldReconnect)
            {
                return;
            }

            // wipe the shared field, but *only* if it is still the cache we were
            // thinking about (once it is null, the next caller will reconnect)
            ReleaseConnection(Interlocked.CompareExchange(ref _multiplexer, null, multiplexer),
                Interlocked.CompareExchange(ref _lockFactory, null, lockFactory),
                Interlocked.CompareExchange(ref _redisDatabase, null, database));
        }
    }
    
    private ValueTask OnRedisErrorAsync(Exception exception, IConnectionMultiplexer multiplexer, IDistributedLockFactory lockFactory, IDatabase database)
    {
        if (_options.UseForceReconnect && (exception is RedisConnectionException or SocketException))
        {
            var shouldReconnect = ShouldForceReconnect();
            if (!shouldReconnect)
            {
                return ValueTask.CompletedTask;
            }
            
            // wipe the shared field, but *only* if it is still the cache we were
            // thinking about (once it is null, the next caller will reconnect)
            return ReleaseConnectionAsync(Interlocked.CompareExchange(ref _multiplexer, null, multiplexer),
                Interlocked.CompareExchange(ref _lockFactory, null, lockFactory),
                Interlocked.CompareExchange(ref _redisDatabase, null, database));
        }

        return ValueTask.CompletedTask;
    }

    private static void ReleaseConnection(IConnectionMultiplexer? multiplexer, IDistributedLockFactory? lockFactory, IDatabase? database)
    {
        if (multiplexer is null && lockFactory is null) 
            return;
        
        try
        {
            DisposeLockFactory(lockFactory);

            if (multiplexer is null)
            {
                return;
            }
            
            multiplexer.Close();
            multiplexer.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
    
    private static async ValueTask ReleaseConnectionAsync(IConnectionMultiplexer? multiplexer, IDistributedLockFactory? lockFactory, IDatabase? database)
    {
        if (multiplexer is null && lockFactory is null) 
            return;
        
        try
        {
            await DisposeLockFactoryAsync(lockFactory);

            if (multiplexer is null)
            {
                return;
            }
            
            await multiplexer.CloseAsync();
            await multiplexer.DisposeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static ValueTask DisposeLockFactoryAsync(IDistributedLockFactory? lockFactory)
    {
        if (lockFactory is not null)
        {
            return lockFactory.DisposeAsync();
        }

        return ValueTask.CompletedTask;
    }
    
    private static void DisposeLockFactory(IDistributedLockFactory? lockFactory)
    {
        lockFactory?.Dispose();
    }

    private byte[] TrySerialize<TValue>(TValue value)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, _jsonSerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing the object of type {Type}", typeof(TValue));
            throw;
        }
    }
    
    private TValue? TryDeserialize<TValue>(byte[]? value)
    {
        if (value is null)
            return default;
        
        try
        {
            return JsonSerializer.Deserialize<TValue>(value, _jsonSerializerOptions) ?? throw new JsonException($"Error deserializing the object of type {typeof(TValue).Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing the object of type {Type}", typeof(TValue).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public Task SetSerializedAsync<TValue>(string key, TValue value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        var serializedValue = TrySerialize(value);
        return SetAsync(key, serializedValue, options, token);
    }
    
    /// <inheritdoc />
    public Task SetSerializedAsync<TValue>(string key, TValue value, CancellationToken token = default)
        => SetSerializedAsync(key, value, _redisExplorerOptions.GetEntryOptions<TValue>(), token);

    /// <inheritdoc />
    public void SetSerialized<TValue>(string key, TValue value, DistributedCacheEntryOptions options)
    {
        var serializedValue = TrySerialize(value);
        Set(key, serializedValue, options);
    }

    /// <inheritdoc />
    public void SetSerialized<TValue>(string key, TValue value)
        => SetSerialized(key, value, _redisExplorerOptions.GetEntryOptions<TValue>());

    /// <inheritdoc />
    public async Task<TValue?> GetDeserializedAsync<TValue>(string key, CancellationToken token = default)
    {
        var value = await GetAsync(key, token);
        return TryDeserialize<TValue>(value);
    }

    /// <inheritdoc />
    public TValue? GetDeserialized<TValue>(string key)
    {
        var value = Get(key);
        return TryDeserialize<TValue>(value);
    }
}
