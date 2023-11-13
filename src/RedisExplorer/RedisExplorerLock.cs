using RedLockNet;

namespace RedisExplorer;

/// <inheritdoc cref="IDistributedLock"/>
[PublicAPI]
public sealed class RedisExplorerLock : IDistributedLock, IEquatable<RedisExplorerLock>
{
    private RedisExplorerLock(IRedLock redLock)
    {
        RedLock = redLock;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a new instance of <see cref="IDistributedLock"/> from <see cref="IRedLock"/>.
    /// </summary>
    /// <param name="redLock">The <see cref="IRedLock"/> to translate.</param>
    /// <returns>A translated <see cref="IDistributedLock"/>.</returns>
    public static IDistributedLock FromRedLock(IRedLock redLock)
        => new RedisExplorerLock(redLock);

    /// <inheritdoc />
    public string Resource => RedLock.Resource;

    /// <inheritdoc />
    public string LockId => RedLock.LockId;

    /// <inheritdoc />
    public bool IsAcquired => RedLock.IsAcquired;

    /// <inheritdoc />
    public DistributedLockStatus Status => TranslateRedLockStatus(RedLock.Status);
    
    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; }

    /// <inheritdoc />
    public int ExtendedCount => RedLock.ExtendCount;

    /// <inheritdoc />
    public DistributedLockInstanceSummary InstanceSummary => new(RedLock.InstanceSummary.Acquired, RedLock.InstanceSummary.Conflicted, RedLock.InstanceSummary.Error);

    /// <summary>
    /// Gets the underlying <see cref="IRedLock"/>.
    /// </summary>
    public IRedLock RedLock { get; }

    /// <summary>
    /// Translates the status.
    /// </summary>
    /// <param name="status">Status to translate.</param>
    /// <returns>Translated status.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static DistributedLockStatus TranslateRedLockStatus(RedLockStatus status)
        => status switch
        {
            RedLockStatus.Unlocked => DistributedLockStatus.Unlocked,
            RedLockStatus.Acquired => DistributedLockStatus.Acquired,
            RedLockStatus.NoQuorum => DistributedLockStatus.NoQuorum,
            RedLockStatus.Conflicted => DistributedLockStatus.Conflicted,
            RedLockStatus.Expired => DistributedLockStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => RedLock.DisposeAsync();

    /// <inheritdoc />
    public void Dispose()
        => RedLock.Dispose();

    /// <summary>
    /// Base implementation of the equatable.
    /// </summary>
    /// <param name="other">Other.</param>
    /// <returns>Whether two locks are equal.</returns>
    public bool Equals(RedisExplorerLock? other)
    {
        if (other is null)
            return false;

        return IsAcquired == other.IsAcquired && Resource == other.Resource && LockId == other.LockId &&
               Status == other.Status;
    }

    /// <summary>
    /// Base implementation of the equatable.
    /// </summary>
    /// <param name="obj">Other.</param>
    /// <returns>Whether two locks are equal.</returns>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((RedisExplorerLock)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(IsAcquired, Resource, LockId, Status);
    }

    /// <summary>
    /// Compares two locks.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(RedisExplorerLock? left, RedisExplorerLock? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Compares two locks.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator !=(RedisExplorerLock? left, RedisExplorerLock? right)
    {
        return !Equals(left, right);
    }
}
