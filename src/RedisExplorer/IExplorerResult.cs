using System.Diagnostics.CodeAnalysis;

namespace RedisExplorer;

/// <summary>
/// Represents a result of an operation against Redis performed with RedisExplorer scripts.
/// </summary>
[PublicAPI]
public interface IExplorerResult
{
    /// <summary>
    /// The key associated with the operation.
    /// </summary>
    string Key { get; }
    
    /// <summary>
    /// The inner result of the operation.
    /// </summary>
    RedisResult RedisResult { get; }
    
    /// <summary>
    /// Flags associated with the result.
    /// </summary>
    ExplorerResultFlags Flags { get; }
    
    /// <summary>
    /// Whether the operation is considered a success - whether the operation has completed with no Redis or process requirement related errors.
    /// </summary>
    /// <remarks>This is a shortcut for checking whether the <see cref="ExplorerResultFlags.Success"/> flag is present in <see cref="Flags"/>.</remarks>
    bool IsSuccess { get; }
    
    /// <summary>
    /// Whether a Redis error occurred.
    /// </summary>
    /// <remarks>This is a shortcut for checking whether the <see cref="ExplorerResultFlags.RedisError"/> flag is present in <see cref="Flags"/>.</remarks>
    bool RedisErrorOccurred { get; }
    
    /// <summary>
    /// Whether a process requirement error occurred (ie. refreshing a key with no sliding expiration set).
    /// </summary>
    /// <remarks>This is a shortcut for checking whether the <see cref="ExplorerResultFlags.ProcessRequirementError"/> flag is present in <see cref="Flags"/>.</remarks>
    bool ProcessRequirementErrorOccurred { get; }
}

/// <summary>
/// Represents a result of an operation against Redis performed with RedisExplorer scripts which may result with a value.
/// </summary>
[PublicAPI]
public interface IExplorerResult<TValue> : IExplorerResult where TValue : class
{
    /// <summary>
    /// Deserialized result value, if any.
    /// </summary>
    TValue? Value { get; }
    
    /// <summary>
    /// Whether the operation is defined - it is a success and it has a non null value.
    /// </summary>
    /// <param name="value">The value.</param>
    [MemberNotNullWhen(true, nameof(Value))]
    bool IsDefined([NotNullWhen(true)] out TValue? value);
}
