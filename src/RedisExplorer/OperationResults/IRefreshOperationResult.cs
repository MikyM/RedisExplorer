namespace RedisExplorer.OperationResults;

/// <summary>
/// Represents a result of a REFRESH operation against Redis performed with RedisExplorer.
/// </summary>
[PublicAPI]
public interface IRefreshOperationResult : IExplorerResult
{
    /// <summary>
    /// Whether the key was not found.
    /// </summary>
    bool KeyNotFound { get; }
 
    /// <summary>
    /// Whether the key has no sliding expiration.
    /// </summary>
    bool KeyHasNoSlidingExpiration { get; }
}
