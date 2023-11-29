namespace RedisExplorer.OperationResults;


/// <inheritdoc cref="ISetResult"/>
/// <inheritdoc cref="ExplorerResult"/>
[PublicAPI]
public class SetResult : ExplorerResult, ISetResult
{
    /// <summary>
    /// Creates a new instance of <see cref="SetResult"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The SE.Redis result.</param>
    public SetResult(string key, RedisResult redisResult) : base(key, redisResult)
    {
    }
    
    /// <inheritdoc/>
    public bool? KeyCollisionOccurred { get; init; }
    
    /// <inheritdoc/>
    public bool KeyOverwritten { get; init; }
}
