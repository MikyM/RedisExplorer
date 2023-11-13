namespace RedisExplorer;

/// <summary>
/// Represents a distributed lock instance summary info.
/// </summary>
[PublicAPI]
public struct DistributedLockInstanceSummary
{
    /// <summary>
    /// Gets the number of acquired locks.
    /// </summary>
    public readonly int Acquired;
    /// <summary>
    /// Gets the number of conflicted locks.
    /// </summary>
    public readonly int Conflicted;
    /// <summary>
    /// Gets the number of errors.
    /// </summary>
    public readonly int Error;

    internal DistributedLockInstanceSummary(int acquired, int conflicted, int error)
    {
        Acquired = acquired;
        Conflicted = conflicted;
        Error = error;
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"Acquired: {Acquired}, Conflicted: {Conflicted}, Error: {Error}";
}
