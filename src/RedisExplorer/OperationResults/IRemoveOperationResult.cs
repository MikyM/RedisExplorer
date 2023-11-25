namespace RedisExplorer.OperationResults;

/// <summary>
/// Represents a result of a REMOVE operation against Redis performed with RedisExplorer.
/// </summary>
[PublicAPI]
public interface IRemoveOperationResult : IExplorerResult
{
    /// <summary>
    /// Whether the key was not found.
    /// </summary>
    bool KeyNotFound { get; }
}
