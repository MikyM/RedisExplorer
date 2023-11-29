namespace RedisExplorer.OperationResults;


/// <inheritdoc cref="IGetResult{TValue}"/>
/// <inheritdoc cref="ExplorerResult"/>
[PublicAPI]
public class GetResult<TValue> : ExplorerResult<TValue>, IGetResult<TValue>
{
    /// <summary>
    /// Creates a new instance of <see cref="GetResult{TValue}"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The SE.Redis result.</param>
    /// <param name="value">The value.</param>
    public GetResult(string key, RedisResult redisResult, TValue? value = default) : base(key, redisResult, value)
    {
    }
    
    /// <inheritdoc/>
    public bool KeyNotFound { get; init; }
}
