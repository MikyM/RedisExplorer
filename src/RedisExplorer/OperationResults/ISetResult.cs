namespace RedisExplorer.OperationResults;

/// <summary>
/// Represents a result of a SET operation against Redis performed with RedisExplorer.
/// </summary>
[PublicAPI]
public interface ISetResult : IExplorerResult
{
    /// <summary>
    /// Whether a key collision occurred.
    /// <para>
    /// This will be non-null only when using the <see cref="SetOptions.OverwriteIfKeyExists"/> false setting on <see cref="SetOptions"/>.
    /// </para>
    /// </summary>
    bool? KeyCollisionOccurred { get; }

    /// <summary>
    /// Whether the operation replaced an existing value.
    /// </summary>
    bool KeyOverwritten { get; }
}
