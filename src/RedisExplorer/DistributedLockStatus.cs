namespace RedisExplorer;

/// <summary>
/// Represents a Redis resource lock status.
/// </summary>
[PublicAPI]
public enum DistributedLockStatus
{
    /// <summary>
    /// Unlocked.
    /// </summary>
    Unlocked,
    /// <summary>
    /// Acquired.
    /// </summary>
    Acquired,
    /// <summary>
    /// NoQuorum.
    /// </summary>
    NoQuorum,
    /// <summary>
    /// Conflicted.
    /// </summary>
    Conflicted,
    /// <summary>
    /// Expired.
    /// </summary>
    Expired,
}
