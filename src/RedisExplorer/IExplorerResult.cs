using System.Diagnostics.CodeAnalysis;

namespace RedisExplorer;

/// <summary>
/// Represents a result of an operation against Redis performed with RedisExplorer.
/// </summary>
[PublicAPI]
public interface IExplorerResult : IEquatable<IExplorerResult>
{
    /// <summary>
    /// The unique identifier of the operation.
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// The key associated with the operation.
    /// </summary>
    string Key { get; }
    
    /// <summary>
    /// The time the result was obtained.
    /// </summary>
    DateTimeOffset ObtainedAt { get; }
    
    /// <summary>
    /// The inner result of the operation.
    /// </summary>
    RedisResult RedisResult { get; }
    
    /// <summary>
    /// Whether the operation is considered a success - whether the operation has completed with no Redis or process requirement related errors.
    /// </summary>
    bool IsSuccess { get; }
    
    /// <summary>
    /// Whether a Redis error occurred.
    /// </summary>
    bool RedisErrorOccurred { get; }
    
    /// <summary>
    /// Whether a process requirement error occurred (ie. refreshing a key with no sliding expiration set).
    /// </summary>
    bool ProcessRequirementErrorOccurred { get; }
}

/// <summary>
/// Represents a result of an operation against Redis performed with RedisExplorer which may contain a value.
/// </summary>
[PublicAPI]
public interface IExplorerResult<TValue> : IExplorerResult
{
    /// <summary>
    /// The value, if any.
    /// </summary>
    TValue? Value { get; }
    
    /// <summary>
    /// Whether the operation is defined - it is a success and it has a non null value.
    /// </summary>
    /// <param name="value">The value.</param>
    [MemberNotNullWhen(true, nameof(Value))]
    bool IsDefined([NotNullWhen(true)] out TValue? value);
}
