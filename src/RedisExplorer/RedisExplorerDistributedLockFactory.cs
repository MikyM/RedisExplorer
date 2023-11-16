using RedLockNet.SERedis;

namespace RedisExplorer;

/// <inheritdoc/>
[PublicAPI]
public sealed class RedisExplorerDistributedLockFactory : IDistributedLockFactory
{
    /// <summary>
    /// Gets the underlying <see cref="RedLockFactory"/>.
    /// </summary>
    public RedLockFactory RedLockFactory { get; }
    
    internal RedisExplorerDistributedLockFactory(RedLockFactory redLockFactory)
    {
        RedLockFactory = redLockFactory;
    }

    /// <inheritdoc/>
    public IDistributedLock CreateLock(string resource, TimeSpan expiryTime)
        => RedLockFactory.CreateLock(resource, expiryTime).ToDistributedLock();

    /// <inheritdoc/>
    public async Task<IDistributedLock> CreateLockAsync(string resource, TimeSpan expiryTime,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        return (await RedLockFactory.CreateLockAsync(resource, expiryTime)).ToDistributedLock();
    }

    /// <inheritdoc/>
    public IDistributedLock CreateLock(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime,
        CancellationToken cancellationToken = default)
        => RedLockFactory.CreateLock(resource, expiryTime, waitTime, retryTime).ToDistributedLock();

    /// <inheritdoc/>
    public async Task<IDistributedLock> CreateLockAsync(string resource, TimeSpan expiryTime, TimeSpan waitTime,
        TimeSpan retryTime,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        return (await RedLockFactory.CreateLockAsync(resource, expiryTime, waitTime, retryTime)).ToDistributedLock();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        RedLockFactory.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        RedLockFactory.Dispose();
        return ValueTask.CompletedTask;
    }
}
