using System.Diagnostics.CodeAnalysis;

namespace RedisExplorer;

/// <inheritdoc/>
[PublicAPI]
public record ExplorerResult(string Key, RedisResult RedisResult, RedisExplorerResultFlags Flags) : IExplorerResult
{
    /// <inheritdoc/>
    public bool IsSuccess => (Flags & RedisExplorerResultFlags.Success) == RedisExplorerResultFlags.Success;

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(ExplorerResult)}: {Key} - {IsSuccess} - {Flags}";
}

/// <inheritdoc cref="IExplorerResult{T}"/>
/// <inheritdoc cref="IExplorerResult"/>
[PublicAPI]
public record ExplorerResult<TValue>(string Key, RedisResult RedisResult, RedisExplorerResultFlags Flags, TValue? Value = null) 
    : ExplorerResult(Key, RedisResult, Flags), IExplorerResult<TValue> where TValue : class
{
    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsDefined([NotNullWhen(true)] out TValue? value)
    {
        value = Value;
        return IsSuccess && value is not null;
    }
}
