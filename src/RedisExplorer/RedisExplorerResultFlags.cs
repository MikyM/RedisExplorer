﻿namespace RedisExplorer;

/// <summary>
/// Result flags.
/// </summary>
[Flags]
[PublicAPI]
public enum RedisExplorerResultFlags
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
}