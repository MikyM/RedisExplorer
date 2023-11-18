using System.Diagnostics.CodeAnalysis;

namespace RedisExplorer;

/// <inheritdoc/>
[PublicAPI]
public record ExplorerResult : IExplorerResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExplorerResult"/> class.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The Redis result.</param>
    /// <param name="flags">The flags.</param>
    public ExplorerResult(string key, RedisResult redisResult, RedisExplorerResultFlags flags)
    {
        Key = key;
        RedisResult = redisResult;
        Flags = flags;
        
        if ((Flags & RedisExplorerResultFlags.Success) == 0)
        {
            Flags |= RedisExplorerResultFlags.Error;
        }
    }

    /// <inheritdoc/>
    public bool IsSuccess => (Flags & RedisExplorerResultFlags.Success) == RedisExplorerResultFlags.Success;
    
    /// <inheritdoc/>
    public string Key { get; init; }
    
    /// <inheritdoc/>
    public RedisResult RedisResult { get; init; }
    
    /// <inheritdoc/>
    public RedisExplorerResultFlags Flags { get; protected init; }

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(ExplorerResult)}: {Key} - {IsSuccess} - {Flags}";
}

/// <inheritdoc cref="IExplorerResult{T}"/>
/// <inheritdoc cref="IExplorerResult"/>
[PublicAPI]
public record ExplorerResult<TValue> : ExplorerResult, IExplorerResult<TValue> where TValue : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExplorerResult{T}"/> class.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The Redis result.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="value">The value.</param>
    public ExplorerResult(string key, RedisResult redisResult, RedisExplorerResultFlags flags, TValue? value = null) : base(key, redisResult, flags)
    {
        if (value is not null)
        {
            Flags |= RedisExplorerResultFlags.NonNullValue;
        }
        
        Value = value;
    }

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsDefined([NotNullWhen(true)] out TValue? value)
    {
        value = Value;
        return IsSuccess && value is not null;
    }

    /// <inheritdoc />
    public TValue? Value { get; init; }
}
