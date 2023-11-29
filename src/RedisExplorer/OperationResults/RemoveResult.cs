namespace RedisExplorer.OperationResults;


/// <inheritdoc cref="IRemoveResult"/>
/// <inheritdoc cref="ExplorerResult"/>
[PublicAPI]
public class RemoveResult : ExplorerResult, IRemoveResult
{
    /// <summary>
    /// Creates a new instance of <see cref="RemoveResult"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The SE.Redis result.</param>
    public RemoveResult(string key, RedisResult redisResult) : base(key, redisResult)
    {
    }
    
    /// <inheritdoc/>
    public bool KeyNotFound { get; init; }
}
