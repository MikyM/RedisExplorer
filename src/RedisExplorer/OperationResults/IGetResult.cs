namespace RedisExplorer.OperationResults;

/// <summary>
/// Represents a result of a GET operation against Redis performed with RedisExplorer.
/// </summary>
[PublicAPI]
public interface IGetResult<TValue> : IExplorerResult<TValue>
{
    /// <summary>
    /// Whether the key was not found.
    /// </summary>
    bool KeyNotFound { get; }
}
