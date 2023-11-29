namespace RedisExplorer.OperationResults;


/// <inheritdoc cref="IRefreshResult"/>
/// <inheritdoc cref="ExplorerResult"/>
[PublicAPI]
public class RefreshResult : ExplorerResult, IRefreshResult
{
    /// <summary>
    /// Creates a new instance of <see cref="RefreshResult"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The SE.Redis result.</param>
    public RefreshResult(string key, RedisResult redisResult) : base(key, redisResult)
    {
    }
    
    /// <inheritdoc/>
    public bool KeyNotFound { get; init; }

    /// <inheritdoc/>
    public bool KeyHasNoSlidingExpiration { get; init; }
}
