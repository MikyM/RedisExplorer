using System.Diagnostics.CodeAnalysis;

namespace RedisExplorer;

/// <inheritdoc cref="IExplorerResult"/>
[PublicAPI]
public abstract class ExplorerResult : IExplorerResult, IEquatable<ExplorerResult>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExplorerResult"/> class.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The Redis result.</param>
    protected ExplorerResult(string key, RedisResult redisResult)
    {
        Key = key;
        RedisResult = redisResult;
        Id = Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public bool IsSuccess => RedisErrorOccurred == false && ProcessRequirementErrorOccurred == false;

    /// <inheritdoc/>
    public bool RedisErrorOccurred { get; init; }
    
    /// <inheritdoc/>
    public bool ProcessRequirementErrorOccurred { get; init; }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Key { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset ObtainedAt { get; }

    /// <inheritdoc/>
    public RedisResult RedisResult { get; init; }

    /// <inheritdoc/>
    public bool Equals(IExplorerResult? other)
        => other is not null && Id == other.Id;

    /// <inheritdoc/>
    public bool Equals(ExplorerResult? other)
        => other is not null && Id == other.Id;
    
    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is IExplorerResult other && Id == other.Id;
    
    /// <inheritdoc/>
    public override int GetHashCode()
        => Id.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(ExplorerResult)}: {Key} - {IsSuccess}";
}

/// <inheritdoc cref="IExplorerResult{T}"/>
/// <inheritdoc cref="IExplorerResult"/>
[PublicAPI]
public abstract class ExplorerResult<TValue> : ExplorerResult, IExplorerResult<TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExplorerResult{T}"/> class.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="redisResult">The Redis result.</param>
    /// <param name="value">The value.</param>
    protected ExplorerResult(string key, RedisResult redisResult, TValue? value = default) : base(key, redisResult)
    {
        Value = value;
    }

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(Value))]
    public virtual bool IsDefined([NotNullWhen(true)] out TValue? value)
    {
        value = Value;
        return IsSuccess && value is not null;
    }

    /// <inheritdoc />
    public TValue? Value { get; init; }
    
    /// <inheritdoc/>
    public override string ToString() => $"{nameof(ExplorerResult)}<{typeof(TValue).Name}>: {Key} - {IsSuccess} - {(Value is null ? "null" : "non-null")}";
}
