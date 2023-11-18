using System.Diagnostics.CodeAnalysis;

namespace RedisExplorer;

/// <summary>
/// Represents a result of an operation obtained from AsExplored methods.
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
    RedisExplorerResultFlags Flags { get; }
    
    /// <summary>
    /// Whether the operation is considered a success.
    /// </summary>
    bool IsSuccess { get; }
}

/// <summary>
/// Represents a result of an operation obtained from AsExplored methods.
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
