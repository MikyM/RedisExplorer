namespace RedisExplorer.OperationResults;


/// <inheritdoc cref="ISetOperationResult"/>
/// <inheritdoc cref="ExplorerResult"/>
[PublicAPI]
public class SetOperationResult : ExplorerResult, ISetOperationResult
{
    /// <summary>
    /// Creates a new instance of <see cref="SetOperationResult"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The SE.Redis result.</param>
    public SetOperationResult(string key, RedisResult redisResult) : base(key, redisResult)
    {
    }
    
    /// <inheritdoc/>
    public bool? KeyCollisionOccurred { get; init; }
    
    /// <inheritdoc/>
    public bool KeyOverwritten { get; init; }
}
