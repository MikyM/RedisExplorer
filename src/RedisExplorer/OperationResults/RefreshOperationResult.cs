namespace RedisExplorer.OperationResults;


/// <inheritdoc cref="IRefreshOperationResult"/>
/// <inheritdoc cref="ExplorerResult"/>
[PublicAPI]
public class RefreshOperationResult : ExplorerResult, IRefreshOperationResult
{
    /// <summary>
    /// Creates a new instance of <see cref="RefreshOperationResult"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The SE.Redis result.</param>
    public RefreshOperationResult(string key, RedisResult redisResult) : base(key, redisResult)
    {
    }
    
    /// <inheritdoc/>
    public bool KeyNotFound { get; init; }

    /// <inheritdoc/>
    public bool KeyHasNoSlidingExpiration { get; init; }
}
