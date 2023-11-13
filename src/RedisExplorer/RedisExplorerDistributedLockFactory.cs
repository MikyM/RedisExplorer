using RedLockNet.SERedis;

namespace RedisExplorer;

/// <inheritdoc/>
[PublicAPI]
public sealed class RedisExplorerDistributedLockFactory : IDistributedLockFactory
{
    private readonly RedLockFactory _redLockFactory;
    
    internal RedisExplorerDistributedLockFactory(RedLockFactory redLockFactory)
    {
        _redLockFactory = redLockFactory;
    }

    /// <inheritdoc/>
    public IDistributedLock CreateLock(string resource, TimeSpan expiryTime)
        => _redLockFactory.CreateLock(resource, expiryTime).ToDistributedLock();

    /// <inheritdoc/>
    public async Task<IDistributedLock> CreateLockAsync(string resource, TimeSpan expiryTime,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        return (await _redLockFactory.CreateLockAsync(resource, expiryTime)).ToDistributedLock();
    }

    /// <inheritdoc/>
    public IDistributedLock CreateLock(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime,
        CancellationToken cancellationToken = default)
        => _redLockFactory.CreateLock(resource, expiryTime, waitTime, retryTime).ToDistributedLock();

    /// <inheritdoc/>
    public async Task<IDistributedLock> CreateLockAsync(string resource, TimeSpan expiryTime, TimeSpan waitTime,
        TimeSpan retryTime,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        return (await _redLockFactory.CreateLockAsync(resource, expiryTime, waitTime, retryTime)).ToDistributedLock();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _redLockFactory.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _redLockFactory.Dispose();
        return ValueTask.CompletedTask;
    }
}
