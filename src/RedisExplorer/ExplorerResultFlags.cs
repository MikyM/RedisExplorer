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
    /// Success.
    /// </summary>
    Success = 1 << 0,
    /// <summary>
    /// An unexpected result was returned.
    /// </summary>
    UnexpectedResultAcquired = 1 << 1,
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
    /// Error.
    /// </summary>
    Error = 1 << 7,
    /// <summary>
    /// Key has no sliding expiration.
    /// </summary>
    NoSlidingExpiration = 1 << 8,
}
