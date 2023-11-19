namespace RedisExplorer;

/// <summary>
/// Result flags.
/// </summary>
[Flags]
[PublicAPI]
public enum ExplorerResultFlags
{
    /// <summary>
    /// None.
    /// </summary>
    None = 0,
    /// <summary>
    /// The operation has completed with no Redis or process requirement related errors.
    /// </summary>
    Success = 1 << 0,
    /// <summary>
    /// The operation outcome is unknown due to an unknown result acquired from process execution.
    /// </summary>
    UnknownOutcome = 1 << 1,
    /// <summary>
    /// The key was not found.
    /// </summary>
    KeyNotFound = 1 << 2,
    /// <summary>
    /// The key already exists.
    /// </summary>
    KeyCollision = 1 << 3,
    /// <summary>
    /// An unknown error occurred.
    /// </summary>
    UnknownErrorOccurred = 1 << 4,
    /// <summary>
    /// The key has been overwritten.
    /// </summary>
    KeyOverwritten = 1 << 5,
    /// <summary>
    /// The result has a non null value.
    /// </summary>
    NonNullValue = 1 << 6,
    /// <summary>
    /// The operation was not completed due to a process requirement.
    /// </summary>
    ProcessRequirementError = 1 << 7,
    /// <summary>
    /// The operation was not completed due to an Error returned by Redis.
    /// </summary>
    RedisError = 1 << 8,
    /// <summary>
    /// Key has no sliding expiration.
    /// </summary>
    NoSlidingExpiration = 1 << 9
}
