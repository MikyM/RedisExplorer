namespace RedisExplorer;

/// <summary>
/// Represents a distributed resource lock.
/// </summary>
[PublicAPI]
public interface IDistributedLock : IAsyncDisposable, IDisposable, IEquatable<IDistributedLock>
{
    /// <summary>
    /// Gets the resource on which the lock was created.
    /// </summary>
    string Resource { get; }
    
    /// <summary>
    /// Gets the lock's ID.
    /// </summary>
    string LockId { get; }
    
    /// <summary>
    /// Gets whether the lock was successfully acquired.
    /// </summary>
    bool IsAcquired { get; }
    
    /// <summary>
    /// Gets the lock's status.
    /// </summary>
    DistributedLockStatus Status { get; }
    
    /// <summary>
    /// Gets the creation date of the lock (UTC) (of the object instance, not of the actual lock Redis-side).
    /// </summary>
    DateTimeOffset CreatedAt { get; }
    
    /// <summary>
    /// Gets the extended count.
    /// </summary>
    int ExtendedCount { get; }
    
    /// <summary>
    /// Gets the instance summary.
    /// </summary>
    DistributedLockInstanceSummary InstanceSummary { get; }

    /// <summary>
    /// Base implementation of the equatable.
    /// </summary>
    /// <param name="other">Other.</param>
    /// <returns>Whether two locks are equal.</returns>
    bool IEquatable<IDistributedLock>.Equals(IDistributedLock? other) =>
        other is not null && Resource == other.Resource &&
        LockId == other.LockId;
}
