namespace RedisExplorer.OperationResults;


/// <inheritdoc cref="IGetOperationResult{T}"/>
/// <inheritdoc cref="ExplorerResult"/>
[PublicAPI]
public class GetOperationResult<TValue> : ExplorerResult<TValue>, IGetOperationResult<TValue>
{
    /// <summary>
    /// Creates a new instance of <see cref="GetOperationResult{T}"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The SE.Redis result.</param>
    /// <param name="value">The value.</param>
    public GetOperationResult(string key, RedisResult redisResult, TValue? value = default) : base(key, redisResult, value)
    {
    }
    
    /// <inheritdoc/>
    public bool KeyNotFound { get; init; }
}
