using JetBrains.Annotations;

namespace RedisExplorer.Results;

/// <summary>
/// Redis error type.
/// </summary>
[PublicAPI]
public enum RedisErrorType
{
    /// <summary>
    /// An unexpected result was returned.
    /// </summary>
    UnexpectedResult,
    /// <summary>
    /// The key was not found.
    /// </summary>
    NotFound,
    /// <summary>
    /// The key already exists.
    /// </summary>
    KeyExists,
    /// <summary>
    /// An unknown error occurred.
    /// </summary>
    Unknown
}
