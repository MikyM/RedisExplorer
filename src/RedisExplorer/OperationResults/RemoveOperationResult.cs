namespace RedisExplorer.OperationResults;


/// <inheritdoc cref="IRemoveOperationResult"/>
/// <inheritdoc cref="ExplorerResult"/>
[PublicAPI]
public class RemoveOperationResult : ExplorerResult, IRemoveOperationResult
{
    /// <summary>
    /// Creates a new instance of <see cref="RemoveOperationResult"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The SE.Redis result.</param>
    public RemoveOperationResult(string key, RedisResult redisResult) : base(key, redisResult)
    {
    }
    
    /// <inheritdoc/>
    public bool KeyNotFound { get; init; }
}
