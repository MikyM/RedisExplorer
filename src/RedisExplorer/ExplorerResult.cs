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
    public ExplorerResult(string key, RedisResult redisResult, ExplorerResultFlags flags)
    {
        Key = key;
        RedisResult = redisResult;
        Flags = flags;
    }

    /// <inheritdoc/>
    public bool IsSuccess => (Flags & ExplorerResultFlags.Success) != 0;

    /// <inheritdoc/>
    public bool RedisErrorOccurred => (Flags & ExplorerResultFlags.RedisError) != 0;
    
    /// <inheritdoc/>
    public bool ProcessRequirementErrorOccurred => (Flags & ExplorerResultFlags.ProcessRequirementError) != 0;

    /// <inheritdoc/>
    public string Key { get; init; }
    
    /// <inheritdoc/>
    public RedisResult RedisResult { get; init; }
    
    /// <inheritdoc/>
    public ExplorerResultFlags Flags { get; protected init; }

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
    public ExplorerResult(string key, RedisResult redisResult, ExplorerResultFlags flags, TValue? value = null) : base(key, redisResult, flags)
    {
        if (value is not null)
        {
            Flags |= ExplorerResultFlags.NonNullValue;
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
    
    /// <inheritdoc/>
    public override string ToString() => $"{nameof(ExplorerResult)}<{typeof(TValue).Name}>: {Key} - {IsSuccess} - {Flags} - {(Value is null ? "null" : "non-null")}";
}
